using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Classification.Models;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Models.Enums;
using ProjectVico.V2.Shared.Responses;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers;

public class ProcessIngestedDocumentConsumer : IConsumer<ProcessIngestedDocument>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IPdfPipeline _nuclearEnvironmentalReportPdfPipeline;
    private readonly IPdfPipeline _baselinePipeline;
    private readonly ILogger<ProcessIngestedDocumentConsumer> _logger;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;


    public ProcessIngestedDocumentConsumer(
        DocGenerationDbContext dbContext,
        [FromKeyedServices("nuclear-er-pipeline")]
        IPdfPipeline nuclearEnvironmentalReportPdfPipeline,
        [FromKeyedServices("baseline-pipeline")]
        IPdfPipeline baselinePipeline,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        ILogger<ProcessIngestedDocumentConsumer> logger)
    
    {
        _dbContext = dbContext;
        _nuclearEnvironmentalReportPdfPipeline = nuclearEnvironmentalReportPdfPipeline;
        _baselinePipeline = baselinePipeline;
        _logger = logger;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }

    public async Task Consume(ConsumeContext<ProcessIngestedDocument> context)
    {
        var ingestedDocument = await _dbContext.IngestedDocuments.FindAsync(context.Message.CorrelationId);
        if (ingestedDocument == null)
        {
            await context.Publish(new IngestedDocumentProcessingFailed(context.Message.CorrelationId));
            return;
        }

        ingestedDocument.IngestionState = IngestionState.Processing;
        await _dbContext.SaveChangesAsync();
        
        var message = context.Message;

        if (message.IngestionType == IngestionType.NRCDocument)
        {
            if (message.ClassificationType != DocumentClassificationType.NrcEnvironmentalReportWithNumberedChapters &&
                _serviceConfigurationOptions.ProjectVicoServices.DocumentIngestion.Classification.ClassifyNRCDocuments)
            {
                _logger.LogWarning("ProcessIngestedDocumentConsumer: Processing stopped because of unsupported classification");
                ingestedDocument.IngestionState = IngestionState.ClassificationUnsupported;
                await _dbContext.SaveChangesAsync();
                await context.Publish(new IngestedDocumentProcessingStoppedByUnsupportedClassification(context.Message.CorrelationId));
                return;
            }
            await ProcessNrcDocument(message, ingestedDocument);
        }
        else
        {

            await ProcessCustomDocument(message, ingestedDocument);
        }

        _logger.LogInformation("ProcessIngestedDocumentConsumer: Document processed {CorrelationId}", context.Message.CorrelationId);
        
        await context.Publish(new IngestedDocumentProcessed(context.Message.CorrelationId));
    }

    private async Task ProcessCustomDocument(ProcessIngestedDocument message, IngestedDocument ingestedDocument)
    {
        _logger.LogInformation("ProcessIngestedDocumentConsumer: Processing custom document {CorrelationId}: {FileName}", message.CorrelationId, ingestedDocument.FileName);
        var blobUrl = message.OriginalDocumentUrl;
        var name = message.FileName;

        var pipelineResponse = await _baselinePipeline.RunAsync(blobUrl, name);
        await CommonProcessing(pipelineResponse, ingestedDocument);
    }

    private async Task ProcessNrcDocument(ProcessIngestedDocument message, IngestedDocument ingestedDocument)
    {
        _logger.LogInformation("ProcessIngestedDocumentConsumer: Processing NRC document {CorrelationId}: {FileName}", message.CorrelationId, ingestedDocument.FileName);
        var blobUrl = message.OriginalDocumentUrl;
        var name = message.FileName;

        var pipelineResponse = await _nuclearEnvironmentalReportPdfPipeline.RunAsync(blobUrl, name);
        await CommonProcessing(pipelineResponse, ingestedDocument);

    }

    private async Task CommonProcessing(IngestionPipelineResponse pipelineResponse, IngestedDocument ingestedDocument)
    {
        if (pipelineResponse.Tables != null && _serviceConfigurationOptions.ProjectVicoServices.DocumentIngestion.ProcessTables)
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

                var bodyTextNode = FindBodyTextNodeAboveTable(contentNodes, table, tablePage, tableBoundingRegion);

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
        
        if (pipelineResponse.Tables != null && _serviceConfigurationOptions.ProjectVicoServices.DocumentIngestion.ProcessTables)
        {
            ingestedDocument.Tables.AddRange(pipelineResponse.Tables);
        }

        await _dbContext.SaveChangesAsync();

        
    }

    private ContentNode? FindBodyTextNodeAboveTable(List<ContentNode> contentNodes, Table table, int tablePage, BoundingRegion tableBoundingRegion)
    {
        ContentNode? bodyTextNode = null;

        var bodyTextNodes = contentNodes.Flatten(node => node.Children)
            .Where(x => x.Type == ContentNodeType.BodyText);

        var samePageNodes = bodyTextNodes.Where(x => x.BoundingRegions.First().Page == tablePage).ToList();
        var earlierPageNodes = bodyTextNodes.Where(x => x.BoundingRegions.First().Page < tablePage).ToList();

        if (samePageNodes.Any())
        {
            // If there are BodyText nodes on the same page as the table, find the one that has the bottom-right Y coordinate [2] directly above
            // the top-left Y coordinate of the table [0].

            var orderedSamePageNodes = samePageNodes.OrderBy(x => x.BoundingRegions.First().BoundingPolygons[2].Y);

            var nodeAboveTable = orderedSamePageNodes.LastOrDefault(
                x => x.BoundingRegions.First().BoundingPolygons[2].Y < tableBoundingRegion.BoundingPolygons[0].Y);

            if (nodeAboveTable == null)
            {
                // This means that the table is the first thing on the page, so we can't place it here. We need to find the last BodyText node on the previous page.
                bodyTextNode = earlierPageNodes.LastOrDefault();
            }
            bodyTextNode = nodeAboveTable;
        }
        else if (earlierPageNodes.Any())
        {
            // If there are no BodyText nodes on the same page as the table, use the last one on an earlier page and insert the table 
            // reference at the end of the text of that one.
            bodyTextNode = earlierPageNodes.LastOrDefault();
        }
        return bodyTextNode;
    }

    private List<ContentNode> CondenseContentTree(List<ContentNode> contentTree)
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
                    var boundingRegions = currentNode.BoundingRegions;
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