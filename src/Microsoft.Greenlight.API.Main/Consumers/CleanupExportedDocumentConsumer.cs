using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;

namespace Microsoft.Greenlight.API.Main.Consumers;

/// <summary>
/// Consumer class for handling CleanupExportedDocument messages.
/// </summary>
public class CleanupExportedDocumentConsumer : IConsumer<CleanupExportedDocument>
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<CleanupExportedDocumentConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanupExportedDocumentConsumer"/> class.
    /// </summary>
    /// <param name="blobServiceClient">The BlobServiceClient instance.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public CleanupExportedDocumentConsumer(
        [FromKeyedServices("blob-docing")]
        BlobServiceClient blobServiceClient,
        DocGenerationDbContext dbContext,
        ILogger<CleanupExportedDocumentConsumer> logger
    )
    {
        _blobServiceClient = blobServiceClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Consumes the CleanupExportedDocument message. Deletes the exported document from the 
    /// blob storage and the database.
    /// </summary>
    /// <param name="context">The context containing the message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<CleanupExportedDocument> context)
    {
        var message = context.Message;

        if (string.IsNullOrEmpty(message.BlobContainer) || string.IsNullOrEmpty(message.FileName))
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
            _logger.LogInformation(
                "File {fileName} was marked for deletion in container {blobContainer}",
                message.FileName,
                message.BlobContainer);

            var deletedFiles = await _dbContext.ExportedDocumentLinks
                .Where(edl => edl.Id == message.ExportedDocumentLinkId)
                .ExecuteDeleteAsync();

            if (deletedFiles == 0)
            {
                _logger.LogWarning(
                    "File with id {documentLinkId} was not found in db.",
                    message.ExportedDocumentLinkId);
            }
            else
            {
                _logger.LogInformation(
                    "File {fileName} and id {documentLinkId} was deleted from db.",
                    message.FileName,
                    message.ExportedDocumentLinkId.ToString());
            }
        }
        else
        {
            _logger.LogWarning(
                "File {fileName} could not be deleted from container {blobContainer}",
                message.FileName,
                message.BlobContainer);
        }
    }
}
