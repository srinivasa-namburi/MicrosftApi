using System.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers.DocumentIngestionSaga;

public class CreateIngestedDocumentConsumer : IConsumer<CreateIngestedDocument>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<CreateIngestedDocumentConsumer> _logger;
    private readonly AzureFileHelper _azureFileHelper;

    public CreateIngestedDocumentConsumer(
        DocGenerationDbContext dbContext,
        ILogger<CreateIngestedDocumentConsumer> logger,
        AzureFileHelper azureFileHelper
        )
    {
        _dbContext = dbContext;
        _logger = logger;
        _azureFileHelper = azureFileHelper;
    }
    public async Task Consume(ConsumeContext<CreateIngestedDocument> context)
    {
        var document = new IngestedDocument
        {
            Id = context.Message.CorrelationId,
            FileName = context.Message.FileName,
            OriginalDocumentUrl = context.Message.OriginalDocumentUrl,
            DocumentProcess = context.Message.DocumentProcessName,
            UploadedByUserOid = context.Message.UploadedByUserOid,
            IngestionState = IngestionState.Uploaded,
            IngestedDate = DateTime.UtcNow
        };

        // Use the using statement to ensure the stream is disposed of correctly
        await using (var documentStream = await _azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(document.OriginalDocumentUrl))
        {
            if (documentStream != null)
            {
                document.FileHash = documentStream.GenerateHashFromStreamAndResetStream();
            }
        }

        // Find & remove any previous iterations of this ingestion process.
        var existingDocuments = await _dbContext.IngestedDocuments.Where(
                d => d.FileHash == document.FileHash
            ).ToListAsync();

        var documentsToDelete = existingDocuments.Where(
            d => d.IngestionState != IngestionState.Complete &&
                 d.IngestionState != IngestionState.ClassificationUnsupported)
            .ToList();

        if (documentsToDelete.Any())
        {
            _logger.LogInformation(
                "CreateIngestedDocumentConsumer: CorrelationId: {CorrelationId} Found earlier, incomplete ingestions for file {FileName} with the same file hash. Overwriting them.", context.Message.CorrelationId, document.FileName);

            foreach (var existingDocument in documentsToDelete)
            {
                var tables = await _dbContext.Tables.Where(t => t.IngestedDocumentId == existingDocument.Id).ToListAsync();
                foreach (var table in tables)
                {
                    var boundingRegions = await _dbContext.BoundingRegions.Where(b => b.TableId == table.Id).ToListAsync();
                    _dbContext.BoundingRegions.RemoveRange(boundingRegions);
                    await _dbContext.SaveChangesAsync();
                    _dbContext.Tables.Remove(table);
                }

                var contentNodes = await _dbContext.ContentNodes.Where(c => c.IngestedDocumentId == existingDocument.Id).ToListAsync();
                await RecursivelyRemoveContentNodesFromDatabase(contentNodes);
            }
            
            _dbContext.IngestedDocuments.RemoveRange(existingDocuments);
            await _dbContext.SaveChangesAsync();
        }

        if (existingDocuments.Count > documentsToDelete.Count)
        {
            // There are existing documents that are complete, so we can't overwrite them. Stop the process.
            _logger.LogWarning
                ("CreateIngestedDocumentConsumer: CorrelationId: {CorrelationId} Found earlier, complete ingestions for file {FileName} with the same file hash. Stopping ingestion.", context.Message.CorrelationId, document.FileName);

            await context.Publish(new IngestedDocumentRejected(context.Message.CorrelationId));
            return;
        }

        _dbContext.IngestedDocuments.Add(document);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("CreateIngestedDocumentConsumer: Ingested document created: {DocumentId}", document.Id);

        await context.Publish(
            new IngestedDocumentCreatedInDatabase(context.Message.CorrelationId)
            {
                FileHash = document.FileHash!
            });
    }

    private async Task RecursivelyRemoveContentNodesFromDatabase(List<ContentNode> contentNodes)
    {
        foreach (var contentNode in contentNodes)
        {
            var children = await _dbContext.ContentNodes.Where(c => c.ParentId == contentNode.Id).ToListAsync();
            var boundingRegions = await _dbContext.BoundingRegions.Where(b => b.ContentNodeId == contentNode.Id).ToListAsync();
            _dbContext.BoundingRegions.RemoveRange(boundingRegions);
            _dbContext.ContentNodes.Remove(contentNode);

            await RecursivelyRemoveContentNodesFromDatabase(children);
        }
    }
}