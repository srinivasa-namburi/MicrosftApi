using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Data.Sql;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers;

public class CleanupExportedDocumentConsumer : IConsumer<CleanupExportedDocument>
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<CleanupExportedDocumentConsumer> _logger;

    public CleanupExportedDocumentConsumer(
        BlobServiceClient blobServiceClient,
        DocGenerationDbContext dbContext,
        ILogger<CleanupExportedDocumentConsumer> logger)
    {
        _blobServiceClient = blobServiceClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CleanupExportedDocument> context)
    {
        var message = context.Message;

        if(string.IsNullOrEmpty(message.BlobContainer) || string.IsNullOrEmpty(message.FileName))
        {
            _logger.LogWarning("BlobContainer or FileName is null or empty. Skipping cleanup.");

            return;
        }

        var blobClient = _blobServiceClient
            .GetBlobContainerClient(message.BlobContainer)
            .GetBlobClient(message.FileName);

        var markedForDeletion = await blobClient.DeleteIfExistsAsync();

        if (markedForDeletion)
        {
            _logger.LogInformation($"File {message.FileName} was marked for deletion in container {message.BlobContainer}");

            var deletedFiles = await _dbContext.ExportedDocumentLinks
                .Where(edl => edl.Id == message.ExportedDocumentLinkId)
                .ExecuteDeleteAsync();

            if (deletedFiles == 0)
            {
                _logger.LogWarning($"File with id {message.ExportedDocumentLinkId.ToString()} was not found in db.");
            }
            else
            {
                _logger.LogInformation($"File {message.FileName} and id {message.ExportedDocumentLinkId.ToString()} was deleted from db.");
            }
        }
        else
        {
            _logger.LogWarning($"File {message.FileName} could not be deleted from container {message.BlobContainer}");
        }
    }
}