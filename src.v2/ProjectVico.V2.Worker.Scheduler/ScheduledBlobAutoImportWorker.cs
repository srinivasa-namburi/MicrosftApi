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

        var taskDelayDefaultMilliseconds = Convert.ToInt32(TimeSpan.FromSeconds(30).TotalMilliseconds);
        var taskDelayAfterImportMilliseconds = Convert.ToInt32(TimeSpan.FromMinutes(2).TotalMilliseconds);
        var taskDelayAfterNullDocumentProcessFound = Convert.ToInt32(TimeSpan.FromMinutes(5).TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var taskDelay = taskDelayDefaultMilliseconds;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogInformation("ScheduledBlobAutoImportWorker ping: {time}", DateTimeOffset.Now);
            }

            foreach (var documentProcess in _options.ProjectVicoServices.DocumentProcesses)
            {
                if (documentProcess == null)
                {
                    _logger.LogWarning("ScheduledBlobAutoImportWorker: No Document Processes exist - delaying execution for 5 minutes");
                    taskDelay = taskDelayAfterNullDocumentProcessFound;
                    break;  
                }

                var container = documentProcess.BlobStorageContainerName;
                var folder = documentProcess.BlobStorageAutoImportFolderName;

                if (folder == null || container == null)
                {
                    _logger.LogWarning("ScheduledBlobAutoImportWorker: Skipping document process {documentProcessName} as it has no auto-import folder or container configured", documentProcess.Name);
                    continue;
                }

                if (NewFilesInContainerPath(container, folder))
                {
                    taskDelay = taskDelayAfterImportMilliseconds;
                    _logger.LogWarning("ScheduleBlobAutoImportWorker: New files found for document process {documentProcessName}. Delaying next run for {taskDelay}ms after submission", documentProcess.Name, taskDelay);
            
                    await publishEndpoint.Publish(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
                    {
                        ContainerName = container,
                        FolderPath = folder,
                        DocumentProcess = documentProcess.Name,

                    }, stoppingToken);
                }
            }
            
            await Task.Delay(taskDelay, stoppingToken);
        }
    }

    private bool NewFilesInContainerPath(string containerName, string folderPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        return containerClient.GetBlobs(prefix: folderPath).Any();
    }
}
