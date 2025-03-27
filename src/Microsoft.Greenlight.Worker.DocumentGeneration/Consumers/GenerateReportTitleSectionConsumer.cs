using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers;

/// <summary>
/// A consumer class for the <see cref="GenerateReportTitleSection"/> message.
/// </summary>
public class GenerateReportTitleSectionConsumer : IConsumer<GenerateReportTitleSection>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<GenerateReportTitleSectionConsumer> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IServiceProvider _sp;

    /// <summary>
    /// Initializes a new instance of the GenerateReportTitleSectionConsumer class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> instance for this class.</param>
    /// <param name="dbContext">The <see cref="DocGenerationDbContext"/> database context.</param>
    /// <param name="sp">The <see cref="IServiceProvider"/> instance for resolving service dependencies.</param>
    /// <param name="publishEndpoint">The <see cref="IPublishEndpoint"/> for publishing generated messages.</param>
    public GenerateReportTitleSectionConsumer(
        ILogger<GenerateReportTitleSectionConsumer> logger,
        DocGenerationDbContext dbContext,
        IServiceProvider sp,
        IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _sp = sp;
    }

    /// <summary>
    /// Consumes the <see cref="GenerateReportTitleSection"/> context.
    /// </summary>
    /// <param name="context">The <see cref="GenerateReportTitleSection"/> context.</param>
    /// <returns>The long running consuming <see cref="Task"/>.</returns>
    public async Task Consume(ConsumeContext<GenerateReportTitleSection> context)
    {
        var message = context.Message;

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(message.ContentNodeJson));
        var contentNode = await JsonSerializer.DeserializeAsync<ContentNode>(stream);

        if (contentNode == null)
        {
            _logger.LogError("Failed to deserialize ContentNodeJson.");
            return;
        }

        var contentNodeGeneratedEvent = new ContentNodeGenerated(message.CorrelationId)
        {
            ContentNodeId = contentNode.Id,
            AuthorOid = message.AuthorOid
        };

        var tableOfContentsString = GeneratePlainTextTableOfContentsFromOutlineJson(message.DocumentOutlineJson);

        var trackedDocument = await _dbContext.GeneratedDocuments
            .FirstOrDefaultAsync(x => x.Id == message.CorrelationId);

        if (trackedDocument == null)
        {
            // Orphaned generation request
            _logger.LogInformation("Orphaned GenerateReportTitleSection detected. Abandoning.");
            return;
        }

        // Start a transaction for atomic operations
        //using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            // Fetch and track the existing ContentNode
            var existingContentNode = await _dbContext.ContentNodes
                .Include(x => x.Children)
                .Include(x => x.ContentNodeSystemItem)
                    .ThenInclude(y=>y!.SourceReferences)
                .FirstOrDefaultAsync(cn => cn.Id == contentNode.Id);

            if (existingContentNode == null)
            {
                _logger.LogError("ContentNode with ID {ContentNodeId} not found.", contentNode.Id);
                return;
            }

            // If any of the direct descendants of the existing ContentNode are of type BodyText, delete them
            var deleteBodyTextContentNodes = existingContentNode.Children
                .Where(x => x.Type == ContentNodeType.BodyText)
                .ToList();

            foreach (var deleteBodyTextContentNode in deleteBodyTextContentNodes)
            {
                existingContentNode.Children.Remove(deleteBodyTextContentNode);
                _dbContext.ContentNodes.Remove(deleteBodyTextContentNode);
            }

            // Set GenerationState to InProgress
            existingContentNode.GenerationState = ContentNodeGenerationState.InProgress;

            // Associate the tracked document with the ContentNode
            existingContentNode.AssociatedGeneratedDocumentId = trackedDocument.Id;

            await _dbContext.SaveChangesAsync();

            // Publish the InProgress event
            await _publishEndpoint.Publish(new ContentNodeGenerationStateChanged(message.CorrelationId)
            {
                ContentNodeId = existingContentNode.Id,
                GenerationState = ContentNodeGenerationState.InProgress
            });

            // Generate body content nodes
            var bodyContentNodes = await GenerateBodyText(
                existingContentNode.Type == ContentNodeType.Title ? "Title" : "Heading",
                sectionNumber: ExtractSectionNumber(existingContentNode.Text),
                sectionTitle: ExtractSectionTitle(existingContentNode.Text),
                tableOfContentsString,
                trackedDocument.DocumentProcess,
                message.MetadataId,
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
                var existingSystemItem = await _dbContext.ContentNodeSystemItems.FindAsync(systemItem.Id);
                if (existingSystemItem == null)
                {
                    await _dbContext.ContentNodeSystemItems.AddAsync(systemItem);
                }
                else
                {
                    _dbContext.Entry(existingSystemItem).CurrentValues.SetValues(systemItem);
                }
            }

            await _dbContext.ContentNodes.AddRangeAsync(bodyContentNodes);
            await _dbContext.SaveChangesAsync();


            existingContentNode.Children.AddRange(bodyContentNodes);

            if (bodyContentNodes.Count == 1)
            {
                contentNodeGeneratedEvent.ContentNodeId = bodyContentNodes[0].Id;
            }

            // Set GenerationState to Completed
            existingContentNode.GenerationState = ContentNodeGenerationState.Completed;
            await _dbContext.SaveChangesAsync();

            // Publish the Completed event
            await _publishEndpoint.Publish(new ContentNodeGenerationStateChanged(message.CorrelationId)
            {
                ContentNodeId = existingContentNode.Id,
                GenerationState = ContentNodeGenerationState.Completed
            });

            // Commit the transaction
            //await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error while updating ContentNode ID {ContentNodeId}.", contentNode.Id);
            // Optionally, implement a retry mechanism or notify the user/admin
            // For example, you can retry a fixed number of times
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "An error occurred while processing ContentNode ID {ContentNodeId}.", contentNode.Id);
            contentNodeGeneratedEvent.IsSuccessful = false;

            // Attempt to set the state to Failed
            try
            {
                var existingContentNode = await _dbContext.ContentNodes
                    .FirstOrDefaultAsync(cn => cn.Id == contentNode.Id);

                if (existingContentNode != null)
                {
                    existingContentNode.GenerationState = ContentNodeGenerationState.Failed;
                    await _dbContext.SaveChangesAsync();

                    await _publishEndpoint.Publish(new ContentNodeGenerationStateChanged(message.CorrelationId)
                    {
                        ContentNodeId = existingContentNode.Id,
                        GenerationState = ContentNodeGenerationState.Failed
                    });
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(
                    innerEx,
                    "Failed to update GenerationState to Failed for ContentNode ID {ContentNodeId}.",
                    contentNode.Id);
            }
        }
        finally
        {
            // Publish the ContentNodeGenerated event
            await context.Publish(contentNodeGeneratedEvent);
        }
    }

    private string ExtractSectionNumber(string text)
    {
        // Implement logic to extract section number from text
        // Example:
        var firstSpaceIndex = text.IndexOf(' ');
        return firstSpaceIndex > 0 ? text.Substring(0, firstSpaceIndex) : string.Empty;
    }

    private string ExtractSectionTitle(string text)
    {
        // Implement logic to extract section title from text
        // Example:
        var firstSpaceIndex = text.IndexOf(' ');
        return firstSpaceIndex > 0 ? text.Substring(firstSpaceIndex).Trim() : text;
    }

    private async Task<List<ContentNode>> GenerateBodyText(string contentNodeType, string sectionNumber,
            string sectionTitle, string tableOfContentsString, string? documentProcessName, 
            Guid? metadataId = null,
            ContentNode? sectionContentNode = null)
    {
        // The documentProcessName is required to get the correct IBodyTextGenerator
        ArgumentNullException.ThrowIfNull(documentProcessName);

        var bodyTextGenerator = _sp.GetRequiredServiceForDocumentProcess<IBodyTextGenerator>(documentProcessName);

        if (bodyTextGenerator == null)
        {
            throw new InvalidOperationException("No valid IBodyTextGenerator was found");
        }

        var result = await bodyTextGenerator.GenerateBodyText(contentNodeType, sectionNumber, sectionTitle,
            tableOfContentsString, documentProcessName, metadataId, sectionContentNode);
        return result;
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
