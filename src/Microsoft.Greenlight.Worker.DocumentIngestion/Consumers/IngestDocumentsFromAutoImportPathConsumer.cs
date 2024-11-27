using Azure;
using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Migrations;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers;

public class IngestDocumentsFromAutoImportPathConsumer : IConsumer<IngestDocumentsFromAutoImportPath>
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<IngestDocumentsFromAutoImportPathConsumer> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly ServiceConfigurationOptions _options;

    public IngestDocumentsFromAutoImportPathConsumer(
        BlobServiceClient blobServiceClient,
        IOptions<ServiceConfigurationOptions> options,
        ILogger<IngestDocumentsFromAutoImportPathConsumer> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
        _options = options.Value;
    }
    public async Task Consume(ConsumeContext<IngestDocumentsFromAutoImportPath> context)
    {
        var ingestPath = "ingest";
        var message = context.Message;

        string documentLibraryShortName;
        string containerName;

        if (message.DocumentLibraryType == DocumentLibraryType.AdditionalDocumentLibrary)
        {
            var documentLibrary =
                await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(message.DocumentLibraryShortName);

            if (documentLibrary == null)
            {
                _logger.LogError("IngestDocumentsFromAutoImportPathConsumer: Encountered auto-import message with unknown document library - aborting import");
                return;
            }

            documentLibraryShortName = documentLibrary.ShortName;
            containerName = documentLibrary.BlobStorageContainerName;
        }
        else
        {
            var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(message.DocumentLibraryShortName);
            if (documentProcess == null)
            {
                _logger.LogError("IngestDocumentsFromAutoImportPathConsumer: Encountered auto-import message with unknown document process - aborting import");
                return;
            }
            documentLibraryShortName = documentProcess.ShortName;
            containerName = documentProcess.BlobStorageContainerName;
        }

        
        var targetContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobsPageable = _blobServiceClient.GetBlobContainerClient(context.Message.BlobContainerName).GetBlobsAsync(prefix: context.Message.FolderPath);

        await foreach (var blobPage in blobsPageable.AsPages())
        {
            blobPage.Values.ToList().ForEach(async blob =>
            {
                var sourceBlobClient = _blobServiceClient.GetBlobContainerClient(context.Message.BlobContainerName).GetBlobClient(blob.Name);

                var todayString = DateTime.Now.ToString("yyyy-MM-dd");

                var newBlobName = $"{ingestPath}/{todayString}/{blob.Name.Replace(message.FolderPath + "/", "")}";
                var targetBlobClient = targetContainerClient.GetBlobClient(newBlobName);

                try
                {
                    await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
                }
                catch (RequestFailedException exception)
                {
                    // If the blob already exists, we can ignore the exception
                    if (exception.Status != 409)
                    {
                        _logger.LogError(exception,
                            "IngestDocumentsFromAutoImportPathConsumer: Failed to copy blob {blobName} from {sourceContainer} to {targetContainer}",
                            blob.Name, context.Message.BlobContainerName, targetContainerClient.Name);
                        return;
                    }
                }
                finally
                {
                    await sourceBlobClient.DeleteIfExistsAsync();
                }

                _logger.LogInformation(
                    context.Message.DocumentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary
                        ? "IngestDocumentsFromAutoImportPathConsumer: Document Process {documentProcess} : Copied blob {blobName} from {sourceContainer} to {targetContainer}"
                        : "IngestDocumentsFromAutoImportPathConsumer: Document Library {DocumentLibraryName} : Copied blob {blobName} from {sourceContainer} to {targetContainer}",
                    message.DocumentLibraryShortName, blob.Name, message.BlobContainerName, targetContainerClient.Name);

                
                var request = new DocumentIngestionRequest()
                {
                    Id = Guid.NewGuid(),
                    OriginalDocumentUrl = targetBlobClient.Uri.ToString(),
                    DocumentLibraryShortName = message.DocumentLibraryShortName,
                    DocumentLibraryType = message.DocumentLibraryType,
                    FileName = targetBlobClient.Uri.Segments.Last()
                };

                await context.Publish(request);
            });
        }
    }
}
