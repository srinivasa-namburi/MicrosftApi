using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.DocumentProcess.Dynamic;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using System.Text;
using System.Text.Json;

namespace Microsoft.Greenlight.Grains.Document;

public class ReportTitleSectionGeneratorGrain : Grain, IReportTitleSectionGeneratorGrain
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly DynamicDocumentProcessServiceFactory _dpServiceFactory;
    private readonly IServiceProvider _sp;
    private readonly ILogger<ReportTitleSectionGeneratorGrain> _logger;

    public ReportTitleSectionGeneratorGrain(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        DynamicDocumentProcessServiceFactory dpServiceFactory,
        IServiceProvider sp,
        ILogger<ReportTitleSectionGeneratorGrain> logger)
    {
        _dbContextFactory = dbContextFactory;
        _dpServiceFactory = dpServiceFactory;
        _sp = sp;
        _logger = logger;
    }

    
    public async Task GenerateSectionAsync(Guid documentId, string? authorOid, string contentNodeJson,
        string documentOutlineJson, Guid? metadataId)
    {
        var dbContext = _dbContextFactory.CreateDbContext();
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(contentNodeJson));
            var contentNode = await JsonSerializer.DeserializeAsync<ContentNode>(stream);

            if (contentNode == null)
            {
                _logger.LogError("Failed to deserialize ContentNodeJson for document {DocumentId}", documentId);
                await NotifyCompletion(documentId, Guid.Empty, false);
                return;
            }

            var trackedDocument = await dbContext.GeneratedDocuments
                .FirstOrDefaultAsync(x => x.Id == documentId);

            if (trackedDocument == null)
            {
                _logger.LogInformation("Orphaned GenerateReportTitleSection detected. Abandoning for document {DocumentId}", documentId);
                await NotifyCompletion(documentId, contentNode.Id, false);
                return;
            }

            try
            {
                // Fetch and track the existing ContentNode
                var existingContentNode = await dbContext.ContentNodes
                    .Include(x => x.Children)
                    .Include(x => x.ContentNodeSystemItem)
                    .ThenInclude(y => y!.SourceReferences)
                    .FirstOrDefaultAsync(cn => cn.Id == contentNode.Id);

                if (existingContentNode == null)
                {
                    _logger.LogError("ContentNode with ID {ContentNodeId} not found for document {DocumentId}",
                        contentNode.Id, documentId);
                    await NotifyCompletion(documentId, contentNode.Id, false);
                    return;
                }

                // If any of the direct descendants of the existing ContentNode are of type BodyText, delete them
                var deleteBodyTextContentNodes = existingContentNode.Children
                    .Where(x => x.Type == ContentNodeType.BodyText)
                    .ToList();

                foreach (var deleteBodyTextContentNode in deleteBodyTextContentNodes)
                {
                    existingContentNode.Children.Remove(deleteBodyTextContentNode);
                    dbContext.ContentNodes.Remove(deleteBodyTextContentNode);
                }

                // Set GenerationState to InProgress
                existingContentNode.GenerationState = ContentNodeGenerationState.InProgress;

                // Associate the tracked document with the ContentNode
                existingContentNode.AssociatedGeneratedDocumentId = trackedDocument.Id;

                await dbContext.SaveChangesAsync();

                // Publish the InProgress event
                await NotifyStateChange(documentId, existingContentNode.Id, ContentNodeGenerationState.InProgress);

                // Generate table of contents
                var tableOfContentsString = GeneratePlainTextTableOfContentsFromOutlineJson(documentOutlineJson);

                var bodyTextGenerator =
                    _dpServiceFactory.GetService<IBodyTextGenerator>(trackedDocument.DocumentProcess!);

                if (bodyTextGenerator == null)
                {
                    _logger.LogError("BodyTextGenerator not found for document process {DocumentProcess}", trackedDocument.DocumentProcess);
                    await NotifyCompletion(documentId, contentNode.Id, false);
                    return;
                }

                var bodyContentNodes = await bodyTextGenerator.GenerateBodyText(
                    existingContentNode.Type == ContentNodeType.Title ? "Title" : "Heading",
                    sectionNumber: ExtractSectionNumber(existingContentNode.Text),
                    sectionTitle: ExtractSectionTitle(existingContentNode.Text),
                    tableOfContentsString,
                    trackedDocument.DocumentProcess,
                    metadataId,
                    existingContentNode
                );

                int bodyContentNodeNumber = 1;
                foreach (var bodyContentNode in bodyContentNodes)
                {
                    bodyContentNode.ParentId = existingContentNode.Id;
                    bodyContentNode.AssociatedGeneratedDocumentId = existingContentNode.AssociatedGeneratedDocumentId;

                    if (bodyContentNodeNumber == 1)
                    {
                        ReAttachContentNodeSystemItemToHeadingNode(bodyContentNode, existingContentNode);
                    }
                    else
                    {
                        bodyContentNode.ContentNodeSystemItemId = null;
                        bodyContentNode.ContentNodeSystemItem = null;
                    }

                    bodyContentNodeNumber++;
                }

                if (existingContentNode.ContentNodeSystemItem != null)
                {
                    var systemItem = existingContentNode.ContentNodeSystemItem;
                    var existingSystemItem = await dbContext.ContentNodeSystemItems.FindAsync(systemItem.Id);
                    if (existingSystemItem == null)
                    {
                        await dbContext.ContentNodeSystemItems.AddAsync(systemItem);
                    }
                    else
                    {
                        dbContext.Entry(existingSystemItem).CurrentValues.SetValues(systemItem);
                    }
                }

                await dbContext.ContentNodes.AddRangeAsync(bodyContentNodes);
                await dbContext.SaveChangesAsync();

                existingContentNode.Children.AddRange(bodyContentNodes);

                // Set GenerationState to Completed
                existingContentNode.GenerationState = ContentNodeGenerationState.Completed;
                await dbContext.SaveChangesAsync();

                // Publish the Completed event
                await NotifyStateChange(documentId, existingContentNode.Id, ContentNodeGenerationState.Completed);

                // Notify success
                await NotifyCompletion(documentId, contentNode.Id, true);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error while updating ContentNode ID {ContentNodeId} for document {DocumentId}",
                    contentNode.Id, documentId);
                await NotifyCompletion(documentId, contentNode.Id, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing section for document {DocumentId}", documentId);

            try
            {
                // Try to get the content node ID from the JSON if possible
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(contentNodeJson));
                var contentNode = await JsonSerializer.DeserializeAsync<ContentNode>(stream);
                var nodeId = contentNode?.Id ?? Guid.Empty;

                // Try to update the state to Failed
                if (nodeId != Guid.Empty)
                {
                    var existingContentNode = await dbContext.ContentNodes
                        .FirstOrDefaultAsync(cn => cn.Id == nodeId);

                    if (existingContentNode != null)
                    {
                        existingContentNode.GenerationState = ContentNodeGenerationState.Failed;
                        await dbContext.SaveChangesAsync();

                        // Notify state change
                        await NotifyStateChange(documentId, nodeId, ContentNodeGenerationState.Failed);
                    }
                }

                await NotifyCompletion(documentId, nodeId, false);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to update GenerationState to Failed for document {DocumentId}", documentId);
                await NotifyCompletion(documentId, Guid.Empty, false);
            }
        }
    }

    private async Task NotifyCompletion(Guid documentId, Guid contentNodeId, bool isSuccessful)
    {
        try
        {
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);
            await orchestrationGrain.OnContentNodeGeneratedAsync(contentNodeId, isSuccessful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying completion for content node {ContentNodeId} in document {DocumentId}",
                contentNodeId, documentId);
        }
    }

    private async Task NotifyStateChange(Guid documentId, Guid contentNodeId, ContentNodeGenerationState state)
    {
        try
        {
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);

            await orchestrationGrain.OnContentNodeStateChangedAsync(contentNodeId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing state change notification for content node {ContentNodeId} in document {DocumentId}",
                contentNodeId, documentId);
        }
    }

    private string ExtractSectionNumber(string text)
    {
        // Implement logic to extract section number from text
        var firstSpaceIndex = text.IndexOf(' ');
        return firstSpaceIndex > 0 ? text.Substring(0, firstSpaceIndex) : string.Empty;
    }

    private string ExtractSectionTitle(string text)
    {
        // Implement logic to extract section title from text
        var firstSpaceIndex = text.IndexOf(' ');
        return firstSpaceIndex > 0 ? text.Substring(firstSpaceIndex).Trim() : text;
    }

    private void ReAttachContentNodeSystemItemToHeadingNode(
        ContentNode bodyContentNode,
        ContentNode existingContentNode)
    {
        if (bodyContentNode.ContentNodeSystemItem != null)
        {
            existingContentNode.ContentNodeSystemItemId = bodyContentNode.ContentNodeSystemItem.Id;
            existingContentNode.ContentNodeSystemItem = bodyContentNode.ContentNodeSystemItem;
            existingContentNode.ContentNodeSystemItem.ContentNodeId = existingContentNode.Id;
            existingContentNode.ContentNodeSystemItem.ContentNode = existingContentNode;

            bodyContentNode.ContentNodeSystemItemId = null;
            bodyContentNode.ContentNodeSystemItem = null;
        }
    }

    private string GeneratePlainTextTableOfContentsFromOutlineJson(string outlineJson)
    {
        var contentNodes = JsonSerializer.Deserialize<List<ContentNode>>(outlineJson);
        return contentNodes == null ? "" : GenerateTableOfContents(contentNodes);
    }

    private string GenerateTableOfContents(IEnumerable<ContentNode> contentNodes)
    {
        var sb = new StringBuilder();
        foreach (var contentNode in contentNodes)
        {
            AppendContentNode(sb, contentNode, 0);
        }
        return sb.ToString();
    }

    private void AppendContentNode(StringBuilder sb, ContentNode contentNode, int depth)
    {
        sb.AppendLine(new string(' ', depth * 2) + contentNode.Text);
        foreach (var child in contentNode.Children)
        {
            AppendContentNode(sb, child, depth + 1);
        }
    }
}