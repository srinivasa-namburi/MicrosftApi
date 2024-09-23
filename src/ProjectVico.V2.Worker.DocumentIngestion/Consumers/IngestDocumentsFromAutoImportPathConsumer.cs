using Azure;
using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Services;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers;

public class IngestDocumentsFromAutoImportPathConsumer : IConsumer<IngestDocumentsFromAutoImportPath>
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<IngestDocumentsFromAutoImportPathConsumer> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly ServiceConfigurationOptions _options;

    public IngestDocumentsFromAutoImportPathConsumer(
        BlobServiceClient blobServiceClient,
        IOptions<ServiceConfigurationOptions> options,
        ILogger<IngestDocumentsFromAutoImportPathConsumer> logger,
        IDocumentProcessInfoService documentProcessInfoService)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
        _options = options.Value;
    }
    public async Task Consume(ConsumeContext<IngestDocumentsFromAutoImportPath> context)
    {
        var ingestPath = "ingest";
        var message = context.Message;

        var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(message.DocumentProcess);
        if (documentProcess == null)
        {
            _logger.LogError("IngestDocumentsFromAutoImportPathConsumer: Encountered auto-import message with unknown document process - aborting import");
            return;
        }
        var targetContainerClient = _blobServiceClient.GetBlobContainerClient(documentProcess.BlobStorageContainerName);
        var blobsPageable = _blobServiceClient.GetBlobContainerClient(context.Message.ContainerName).GetBlobsAsync(prefix: context.Message.FolderPath);

        await foreach (var blobPage in blobsPageable.AsPages())
        {
            blobPage.Values.ToList().ForEach(async blob =>
            {
                var sourceBlobClient = _blobServiceClient.GetBlobContainerClient(context.Message.ContainerName).GetBlobClient(blob.Name);

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
                            blob.Name, context.Message.ContainerName, targetContainerClient.Name);
                        return;
                    }
                }
                finally
                {
                    await sourceBlobClient.DeleteIfExistsAsync();
                }

                _logger.LogInformation("IngestDocumentsFromAutoImportPathConsumer: Document Process {documentProcess} : Copied blob {blobName} from {sourceContainer} to {targetContainer}", message.DocumentProcess, blob.Name, message.ContainerName, targetContainerClient.Name);

                var request = new DocumentIngestionRequest()
                {
                    Id = Guid.NewGuid(),
                    OriginalDocumentUrl = targetBlobClient.Uri.ToString(),
                    DocumentProcessName = message.DocumentProcess,
                    FileName = targetBlobClient.Uri.Segments.Last()
                };

                await context.Publish(request);
            });
        }
    }
}