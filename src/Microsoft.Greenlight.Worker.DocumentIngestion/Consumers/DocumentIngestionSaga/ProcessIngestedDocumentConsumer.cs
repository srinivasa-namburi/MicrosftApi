using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.DocumentProcess.Shared.Ingestion.Pipelines;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Responses;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers.DocumentIngestionSaga;

/// <summary>
/// Consumer class for processing ingested documents.
/// </summary>
public class ProcessIngestedDocumentConsumer : IConsumer<ProcessIngestedDocument>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<ProcessIngestedDocumentConsumer> _logger;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessIngestedDocumentConsumer"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public ProcessIngestedDocumentConsumer(
        DocGenerationDbContext dbContext,
        IOptionsSnapshot<ServiceConfigurationOptions> serviceConfigurationOptions,
        ILogger<ProcessIngestedDocumentConsumer> logger,
        IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Consumes the specified context.
    /// </summary>
    /// <param name="context">The consume context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<ProcessIngestedDocument> context)
    {
        var message = context.Message;
        var scope = _serviceProvider.CreateScope();

        var documentProcessOptions = _serviceConfigurationOptions.GreenlightServices.DocumentProcesses.SingleOrDefault(x => x?.Name == message.DocumentProcessName);

        if (documentProcessOptions == null)
        {
            _logger.LogWarning("ProcessIngestedDocumentConsumer: Document process options not found for {ProcessName}", message.DocumentProcessName);
            await context.Publish(new IngestedDocumentProcessingFailed(message.CorrelationId));
            return;
        }

        // If a plugin owns the document, we need to get the pipeline for that plugin.
        // Otherwise, we get the pipeline for the document process.

        IPdfPipeline pipeline;
        if (message.Plugin == null)
        {
            pipeline = scope.ServiceProvider.GetRequiredKeyedService<IPdfPipeline>(message.DocumentProcessName + "-IPdfPipeline");
        }
        else
        {
            pipeline = scope.ServiceProvider.GetRequiredKeyedService<IPdfPipeline>(message.Plugin + "-Plugin-IPdfPipeline");
        }

        var ingestedDocument = await _dbContext.IngestedDocuments.FindAsync(context.Message.CorrelationId);
        if (ingestedDocument == null)
        {
            _logger.LogWarning("ProcessIngestedDocumentConsumer: Ingested document not found {CorrelationId}, aborting process", context.Message.CorrelationId);
            await context.Publish(new IngestedDocumentProcessingFailed(context.Message.CorrelationId));
            return;
        }

        ingestedDocument.IngestionState = IngestionState.Processing;
        await _dbContext.SaveChangesAsync();

        await ProcessDocument(message, ingestedDocument, documentProcessOptions, pipeline);

        if (ingestedDocument.IngestionState == IngestionState.ClassificationUnsupported)
        {
            _logger.LogWarning("ProcessIngestedDocumentConsumer: Processing stopped because of unsupported classification");
            await context.Publish(new IngestedDocumentProcessingStoppedByUnsupportedClassification(context.Message.CorrelationId));
            return;
        }

        _logger.LogInformation("ProcessIngestedDocumentConsumer: Document processed {CorrelationId}", context.Message.CorrelationId);

        await context.Publish(new IngestedDocumentProcessed(context.Message.CorrelationId));
    }

    /// <summary>
    /// Processes the document.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="ingestedDocument">The ingested document.</param>
    /// <param name="documentProcessOptions">The document process options.</param>
    /// <param name="pipeline">The PDF pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessDocument(
        ProcessIngestedDocument message,
        IngestedDocument ingestedDocument,
        DocumentProcessOptions documentProcessOptions,
        IPdfPipeline pipeline)
    {
        _logger.LogInformation("ProcessIngestedDocumentConsumer: Processing {documentProcessName} document {CorrelationId}: {FileName}", documentProcessOptions!.Name, message.CorrelationId, ingestedDocument.FileName);

        var pipelineResponse = await pipeline.RunAsync(ingestedDocument, documentProcessOptions);

        if (pipelineResponse.UnsupportedClassification)
        {
            ingestedDocument.IngestionState = IngestionState.ClassificationUnsupported;
            _dbContext.Update(ingestedDocument);
            await _dbContext.SaveChangesAsync();
        }

        await CommonProcessing(pipelineResponse, ingestedDocument);
    }

    /// <summary>
    /// Common processing logic for the pipeline response and ingested document.
    /// </summary>
    /// <param name="pipelineResponse">The pipeline response.</param>
    /// <param name="ingestedDocument">The ingested document.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CommonProcessing(IngestionPipelineResponse pipelineResponse, IngestedDocument ingestedDocument)
    {
        if (pipelineResponse.Tables != null && _serviceConfigurationOptions.GreenlightServices.DocumentIngestion.ProcessTables)
        {
            _logger.LogInformation("ProcessIngestedDocumentConsumer: Processing tables for document {CorrelationId}", ingestedDocument.Id);

            // We save here to gain IDs for every part of the Table, so we can reference them in the ContentNode Text.

            await _dbContext.Tables.AddRangeAsync(pipelineResponse.Tables);
            await _dbContext.SaveChangesAsync();

            foreach (var table in pipelineResponse.Tables)
            {
                // For Each Table, we need to find the BodyText ContentNode that is directly above it.
                // At the end of the Text property of that ContentNode, we need to add a reference to the Table, 
                // like so : "[TABLE_REFERENCE: {TableId}]"
                // We need to recursively traverse the Children of the ContentNode to find the BodyText node that is directly above the Table.


                var contentNodes = pipelineResponse.ContentNodes;
                var tableBoundingRegions = table.BoundingRegions;
                var tableBoundingRegion = tableBoundingRegions.First();
                var tablePage = tableBoundingRegion.Page;

                var bodyTextNode = FindBodyTextNodeAboveTable(contentNodes, tablePage, tableBoundingRegion);

                if (bodyTextNode == null)
                {
                    // We couldn't find a BodyText node directly above the Table, so we can't place this table.
                }
                else
                {
                    bodyTextNode.Text += $"\n\n[TABLE_REFERENCE: {table.Id}]";
                }
            }
        }

        _logger.LogInformation("ProcessIngestedDocumentConsumer: Saving processed content for document {CorrelationId}", ingestedDocument.Id);
        var condensedContentTree = CondenseContentTree(pipelineResponse.ContentNodes);
        await _dbContext.ContentNodes.AddRangeAsync(condensedContentTree);

        await _dbContext.SaveChangesAsync();

        ingestedDocument!.IngestionState = IngestionState.Complete;
        ingestedDocument.ContentNodes.AddRange(condensedContentTree);

        if (pipelineResponse.Tables != null && _serviceConfigurationOptions.GreenlightServices.DocumentIngestion.ProcessTables)
        {
            ingestedDocument.Tables.AddRange(pipelineResponse.Tables);
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Finds the body text node directly above the table.
    /// </summary>
    /// <param name="contentNodes">The content nodes.</param>
    /// <param name="tablePage">The table page.</param>
    /// <param name="tableBoundingRegion">The table bounding region.</param>
    /// <returns>The body text node above the table, if found; otherwise, null.</returns>
    private static ContentNode? FindBodyTextNodeAboveTable(List<ContentNode> contentNodes, int tablePage, BoundingRegion tableBoundingRegion)
    {
        ContentNode? bodyTextNode = null;

        var bodyTextNodes = contentNodes.Flatten(node => node.Children)
            .Where(x => x.Type == ContentNodeType.BodyText &&
                        x.BoundingRegions?.FirstOrDefault()?.BoundingPolygons?.Count > 2);

        var samePageNodes = bodyTextNodes.Where(x => x.BoundingRegions!.First().Page == tablePage).ToList();
        var earlierPageNodes = bodyTextNodes.Where(x => x.BoundingRegions!.First().Page < tablePage).ToList();

        if (samePageNodes.Count > 0)
        {
            // If there are BodyText nodes on the same page as the table, find the one that has the bottom-right Y coordinate [2] directly above
            // the top-left Y coordinate of the table [0].

            var orderedSamePageNodes = samePageNodes.OrderBy(x => x.BoundingRegions!.First().BoundingPolygons![2].Y);

            var nodeAboveTable = orderedSamePageNodes.LastOrDefault(
                x => x.BoundingRegions!.First().BoundingPolygons![2].Y < tableBoundingRegion.BoundingPolygons![0].Y);

            if (nodeAboveTable == null)
            {
                // This means that the table is the first thing on the page, so we can't place it here. We need to find the last BodyText node on the previous page.
                bodyTextNode = earlierPageNodes.LastOrDefault();
            }
            bodyTextNode = nodeAboveTable;
        }
        else if (earlierPageNodes.Count > 0)
        {
            bodyTextNode = earlierPageNodes.LastOrDefault();
        }
        return bodyTextNode;
    }

    /// <summary>
    /// Condenses the content tree by merging contiguous body text nodes.
    /// </summary>
    /// <param name="contentTree">The content tree.</param>
    /// <returns>The condensed content tree.</returns>
    private static List<ContentNode> CondenseContentTree(List<ContentNode> contentTree)
    {
        var condensedTree = new List<ContentNode>();

        for (int i = 0; i < contentTree.Count; i++)
        {
            ContentNode currentNode = contentTree[i];

            if (currentNode.Type == ContentNodeType.Title || currentNode.Type == ContentNodeType.Heading)
            {
                var copyNode = new ContentNode
                {
                    Text = currentNode.Text,
                    Type = currentNode.Type,
                    Parent = currentNode.Parent,
                    BoundingRegions = currentNode.BoundingRegions,
                    Children = CondenseContentTree(currentNode.Children)
                };
                condensedTree.Add(copyNode);
            }
            else if (currentNode.Type == ContentNodeType.BodyText)
            {
                ContentNode? parentNode = currentNode.Parent;

                if (parentNode != null)
                {
                    // Merge contiguous body text nodes
                    var mergedText = currentNode.Text;
                    var j = i + 1;
                    while (j < contentTree.Count && contentTree[j].Type == ContentNodeType.BodyText)
                    {
                        mergedText += " " + contentTree[j].Text;
                        j++;
                    }

                    // New ContentNode with merged text
                    var mergedNode = new ContentNode
                    {
                        Text = mergedText,
                        Type = ContentNodeType.BodyText,
                        BoundingRegions = currentNode.BoundingRegions,
                        Parent = parentNode
                    };
                    condensedTree.Add(mergedNode);

                    i = j - 1; // Move the index to the last processed body text node

                }
                else
                {
                    condensedTree.Add(currentNode);
                }
            }
        }

        return condensedTree;
    }
}
