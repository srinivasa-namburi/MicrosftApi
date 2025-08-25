// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts;
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
        ConcurrencyLease? lease = null;
        var coordinator = GrainFactory.GetGrain<IGlobalConcurrencyCoordinatorGrain>(ConcurrencyCategory.Generation.ToString());
        try
        {
            _logger.LogInformation("Starting section generation for document {DocumentId}", documentId);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(contentNodeJson));
            var contentNode = await JsonSerializer.DeserializeAsync<ContentNode>(stream);

            if (contentNode == null)
            {
                _logger.LogError("Failed to deserialize ContentNodeJson for document {DocumentId}", documentId);
                await NotifyCompletion(documentId, Guid.Empty, false);
                return;
            }

            _logger.LogDebug("Deserialized content node {ContentNodeId} for document {DocumentId}", contentNode.Id, documentId);

            var trackedDocument = await dbContext.GeneratedDocuments
                .FirstOrDefaultAsync(x => x.Id == documentId);

            if (trackedDocument == null)
            {
                _logger.LogInformation("Orphaned GenerateReportTitleSection detected. Abandoning for document {DocumentId}", documentId);
                await NotifyCompletion(documentId, contentNode.Id, false);
                return;
            }

            // Acquire a global generation lease (weight=1) to throttle across the cluster
            var requesterId = $"ReportTitleSection:{documentId}:{contentNode.Id}";
            lease = await coordinator.AcquireAsync(requesterId, weight: 1, waitTimeout: TimeSpan.FromHours(4), leaseTtl: TimeSpan.FromHours(1));

            _logger.LogInformation("Processing content node {ContentNodeId} for document process {DocumentProcess}", 
                contentNode.Id, trackedDocument.DocumentProcess);

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

                _logger.LogDebug("Found existing content node {ContentNodeId}, processing...", existingContentNode.Id);

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

                _logger.LogDebug("Set content node {ContentNodeId} to InProgress state", existingContentNode.Id);

                // Generate table of contents
                var tableOfContentsString = GeneratePlainTextTableOfContentsFromOutlineJson(documentOutlineJson);

                _logger.LogDebug("Getting body text generator for document process {DocumentProcess}", trackedDocument.DocumentProcess);

                var bodyTextGenerator =
                    await _dpServiceFactory.GetServiceAsync<IBodyTextGenerator>(trackedDocument.DocumentProcess!);

                if (bodyTextGenerator == null)
                {
                    _logger.LogError("BodyTextGenerator not found for document process {DocumentProcess}", trackedDocument.DocumentProcess);
                    await NotifyCompletion(documentId, contentNode.Id, false);
                    return;
                }

                _logger.LogInformation("Body text generator obtained successfully for document process {DocumentProcess}, generating body text...", 
                    trackedDocument.DocumentProcess);

                var bodyContentNodes = await bodyTextGenerator.GenerateBodyText(
                    existingContentNode.Type == ContentNodeType.Title ? "Title" : "Heading",
                    sectionNumber: ExtractSectionNumber(existingContentNode.Text),
                    sectionTitle: ExtractSectionTitle(existingContentNode.Text),
                    tableOfContentsString,
                    trackedDocument.DocumentProcess,
                    metadataId,
                    existingContentNode
                );

                _logger.LogInformation("Generated {BodyContentNodeCount} body content nodes for content node {ContentNodeId}", 
                    bodyContentNodes.Count, existingContentNode.Id);

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

                _logger.LogInformation("Successfully completed content generation for node {ContentNodeId} in document {DocumentId}", 
                    existingContentNode.Id, documentId);

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

                await NotifyCompletion(documentId, Guid.Empty, false);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to update GenerationState to Failed for document {DocumentId}", documentId);
                await NotifyCompletion(documentId, Guid.Empty, false);
            }
        }
        finally
        {
            // Release the global lease if it was acquired
            if (lease != null)
            {
                try
                {
                    var released = await coordinator.ReleaseAsync(lease.LeaseId);
                    _logger.LogDebug("Released generation lease {LeaseId} for document {DocumentId}: {Released}", lease.LeaseId, documentId, released);
                }
                catch (Exception releaseEx)
                {
                    _logger.LogError(releaseEx, "Error releasing generation lease for document {DocumentId}", documentId);
                }
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