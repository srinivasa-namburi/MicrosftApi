using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Worker.Scheduler;

public class ScheduledBlobAutoImportWorker : BackgroundService
{
    private readonly ILogger<ScheduledBlobAutoImportWorker> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IServiceProvider _sp;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly ServiceConfigurationOptions _options;


    public ScheduledBlobAutoImportWorker(
        ILogger<ScheduledBlobAutoImportWorker> logger,
        IOptions<ServiceConfigurationOptions> options,
        BlobServiceClient blobServiceClient,
        IServiceProvider sp,
        IDocumentProcessInfoService documentProcessInfoService
        
       )
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _sp = sp;
        _documentProcessInfoService = documentProcessInfoService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.GreenlightServices.DocumentIngestion.ScheduledIngestion == false)
        {
            _logger.LogWarning("ScheduledBlobAutoImportWorker: Scheduled ingestion is disabled in configuration. Exiting worker.");
            return;
        }
        var scope = _sp.CreateScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var taskDelayDefaultMilliseconds = Convert.ToInt32(TimeSpan.FromSeconds(30).TotalMilliseconds);
        var taskDelayAfterImportMilliseconds = Convert.ToInt32(TimeSpan.FromMinutes(2).TotalMilliseconds);
        var taskDelayAfterNullDocumentProcessFound = Convert.ToInt32(TimeSpan.FromMinutes(5).TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var taskDelay = taskDelayDefaultMilliseconds;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("ScheduledBlobAutoImportWorker ping: {time}", DateTimeOffset.Now);
            }

            var documentProcesses = await _documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();

            if (documentProcesses.Count == 0)
            {
                _logger.LogWarning("ScheduledBlobAutoImportWorker: No Document Processes exist - delaying execution for 5 minutes");
                taskDelay = taskDelayAfterNullDocumentProcessFound;
                break;  
            }

            foreach (var documentProcess in documentProcesses)
            {
                var container = documentProcess.BlobStorageContainerName;
                var folder = documentProcess.BlobStorageAutoImportFolderName;

                if (folder == null || container == null)
                {
                    _logger.LogWarning("ScheduledBlobAutoImportWorker: Skipping document process {documentProcessName} as it has no auto-import folder or container configured", documentProcess.ShortName);
                    continue;
                }

                if (NewFilesInContainerPath(container, folder))
                {
                    taskDelay = taskDelayAfterImportMilliseconds;
                    _logger.LogWarning("ScheduleBlobAutoImportWorker: New files found for document process {documentProcessName}. Delaying next run for {taskDelay}ms after submission", documentProcess.ShortName, taskDelay);
            
                    await publishEndpoint.Publish(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
                    {
                        ContainerName = container,
                        FolderPath = folder,
                        DocumentProcess = documentProcess.ShortName,

                    }, stoppingToken);
                }
            }
            
            await Task.Delay(taskDelay, stoppingToken);
        }
    }

    private bool NewFilesInContainerPath(string containerName, string folderPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // If container or folder does not exist, create it
        if (!containerClient.Exists())
        {
            containerClient.CreateIfNotExists();
        }

        return containerClient.GetBlobs(prefix: folderPath).Any();
    }
}
