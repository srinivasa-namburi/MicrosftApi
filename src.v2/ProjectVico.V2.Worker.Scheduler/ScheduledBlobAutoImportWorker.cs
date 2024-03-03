using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Models.Enums;

namespace ProjectVico.V2.Worker.Scheduler;

public class ScheduledBlobAutoImportWorker : BackgroundService
{
    private readonly ILogger<ScheduledBlobAutoImportWorker> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IServiceProvider _sp;
    private readonly ServiceConfigurationOptions _options;


    public ScheduledBlobAutoImportWorker(
        ILogger<ScheduledBlobAutoImportWorker> logger,
        IOptions<ServiceConfigurationOptions> options,
        
        BlobServiceClient blobServiceClient,
        IServiceProvider sp
       )
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _sp = sp;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = _sp.CreateScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var taskDelayDefaultMilliseconds = 30000;
        var taskDelayAfterImportMilliseconds = 120000;

        while (!stoppingToken.IsCancellationRequested)
        {
            var taskDelay = taskDelayDefaultMilliseconds;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogInformation("ScheduledBlobAutoImportWorker ping: {time}", DateTimeOffset.Now);
            }

            var nrcContainer = _options.ProjectVicoServices.DocumentIngestion.ContainerNRC;
            var customDataContainer = _options.ProjectVicoServices.DocumentIngestion.ContainerCustomData;
            var nrcFolder = _options.ProjectVicoServices.DocumentIngestion.FolderAutoImportNRC;
            var customDataFolder = _options.ProjectVicoServices.DocumentIngestion.FolderAutoImportCustomData;

            if (NewFilesInContainerPath(nrcContainer, nrcFolder))
            {
                taskDelay = taskDelayAfterImportMilliseconds;
                _logger.LogWarning("ScheduleBlobAutoImportWorker: New NRC files found. Delaying next run for {taskDelay}ms after submission", taskDelay);
                await publishEndpoint.Publish(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
                {
                    ContainerName = nrcContainer,
                    FolderPath = nrcFolder,
                    IngestionType = IngestionType.NRCDocument
                }, stoppingToken);
            }

            if (NewFilesInContainerPath(customDataContainer, customDataFolder))
            {
                taskDelay = taskDelayAfterImportMilliseconds;
                _logger.LogWarning("ScheduleBlobAutoImportWorker: New Custom Data files found. Delaying next run for {taskDelay}ms after submission", taskDelay);
                await publishEndpoint.Publish(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
                {
                    ContainerName = customDataContainer,
                    FolderPath = customDataFolder,
                    IngestionType = IngestionType.CustomData
                }, stoppingToken);
            }

            // Wait for the specified delay before checking again
            // We wait for longer after an import to give the system time to process the new files (spefically moving them to the ingest folder)

            await Task.Delay(taskDelay, stoppingToken);
        }
    }

    private bool NewFilesInContainerPath(string containerName, string folderPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        
        // Find the number of items matching the folder path pattern (begins with the folderPath inside the container)
        // Returned as a pageable result - but we only need to look at the first one
        var blobItems = containerClient.GetBlobs(prefix: folderPath);
        return blobItems.Any();
    }
}
