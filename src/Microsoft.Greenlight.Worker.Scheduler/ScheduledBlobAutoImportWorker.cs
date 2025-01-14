using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Worker.Scheduler;

/// <summary>
/// Worker service that handles scheduled blob auto-import tasks.
/// </summary>
public class ScheduledBlobAutoImportWorker : BackgroundService
{
    private readonly ILogger<ScheduledBlobAutoImportWorker> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IServiceProvider _sp;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly ServiceConfigurationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledBlobAutoImportWorker"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The service configuration options.</param>
    /// <param name="blobServiceClient">The blob service client.</param>
    /// <param name="sp">The service provider.</param>
    /// <param name="documentProcessInfoService">The document process info service.</param>
    /// <param name="documentLibraryInfoService">The document library info service.</param>
    public ScheduledBlobAutoImportWorker(
        ILogger<ScheduledBlobAutoImportWorker> logger,
        IOptions<ServiceConfigurationOptions> options,
        BlobServiceClient blobServiceClient,
        IServiceProvider sp,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _sp = sp;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
        _options = options.Value;
    }

    /// <summary>
    /// Executes the scheduled blob auto-import tasks.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.GreenlightServices.DocumentIngestion.ScheduledIngestion == false)
        {
            _logger.LogWarning("ScheduledBlobAutoImportWorker: Scheduled ingestion is disabled in configuration. Exiting worker.");
            return;
        }
        using var scope = _sp.CreateScope();
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

            var taskDelayDocumentProcesses = await ProcessBlobsForDocumentProcesses(stoppingToken, taskDelayAfterNullDocumentProcessFound, taskDelayAfterImportMilliseconds, publishEndpoint, taskDelay);
            var taskDelayDocumentLibraries = await ProcessBlobsForDocumentLibraries(stoppingToken, taskDelayAfterNullDocumentProcessFound, taskDelayAfterImportMilliseconds, publishEndpoint, taskDelay);

            taskDelay = Math.Max(taskDelayDocumentProcesses, taskDelayDocumentLibraries);

            await Task.Delay(taskDelay, stoppingToken);
        }
    }

    /// <summary>
    /// Processes blobs for document processes.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token.</param>
    /// <param name="taskDelayAfterNullDocumentProcessFound">The delay after no document process is found.</param>
    /// <param name="taskDelayAfterImportMilliseconds">The delay after import in milliseconds.</param>
    /// <param name="publishEndpoint">The publish endpoint.</param>
    /// <param name="taskDelay">The task delay.</param>
    /// <returns>A task that represents the asynchronous operation, with a result of the task delay.</returns>
    private async Task<int> ProcessBlobsForDocumentProcesses(CancellationToken stoppingToken,
        int taskDelayAfterNullDocumentProcessFound, int taskDelayAfterImportMilliseconds, IPublishEndpoint publishEndpoint,
        int taskDelay)
    {
        var documentProcesses = await _documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();

        if (documentProcesses.Count == 0)
        {
            _logger.LogWarning("ScheduledBlobAutoImportWorker: No Document Processes exist - delaying execution for 5 minutes");
            taskDelay = taskDelayAfterNullDocumentProcessFound;
            return taskDelay;
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
                    BlobContainerName = container,
                    FolderPath = folder,
                    DocumentLibraryShortName = documentProcess.ShortName,
                    DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary
                }, stoppingToken);
            }
        }

        return taskDelay;
    }

    /// <summary>
    /// Processes blobs for document libraries.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token.</param>
    /// <param name="taskDelayAfterNullDocumentProcessFound">The delay after no document process is found.</param>
    /// <param name="taskDelayAfterImportMilliseconds">The delay after import in milliseconds.</param>
    /// <param name="publishEndpoint">The publish endpoint.</param>
    /// <param name="taskDelay">The task delay.</param>
    /// <returns> A task that represents the asynchronous operation, with a result of the task delay.</returns>
    private async Task<int> ProcessBlobsForDocumentLibraries(CancellationToken stoppingToken,
        int taskDelayAfterNullDocumentProcessFound, int taskDelayAfterImportMilliseconds,
        IPublishEndpoint publishEndpoint,
        int taskDelay)
    {
        var documentLibraries = await _documentLibraryInfoService.GetAllDocumentLibrariesAsync();
        if (documentLibraries.Count == 0)
        {
            _logger.LogWarning("ScheduledBlobAutoImportWorker: No Document Libraries exist - delaying execution for 5 minutes");
            taskDelay = taskDelayAfterNullDocumentProcessFound;
            return taskDelay;
        }
        foreach (var documentLibrary in documentLibraries)
        {
            var container = documentLibrary.BlobStorageContainerName;
            var folder = documentLibrary.BlobStorageAutoImportFolderName;

            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(container))
            {
                _logger.LogWarning("ScheduledBlobAutoImportWorker: Skipping document library {documentLibraryName} as it has no auto-import folder or container configured", documentLibrary.ShortName);
                continue;
            }
            if (NewFilesInContainerPath(container, folder))
            {
                taskDelay = taskDelayAfterImportMilliseconds;
                _logger.LogWarning("ScheduleBlobAutoImportWorker: New files found for document library {documentLibraryName}. Delaying next run for {taskDelay}ms after submission", documentLibrary.ShortName, taskDelay);
                await publishEndpoint.Publish(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
                {
                    BlobContainerName = container,
                    FolderPath = folder,
                    DocumentLibraryShortName = documentLibrary.ShortName,
                    DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary
                }, stoppingToken);
            }
        }
        return taskDelay;
    }

    /// <summary>
    /// Checks if there are new files in the specified container path.
    /// </summary>
    /// <param name="containerName">The container name.</param>
    /// <param name="folderPath">The folder path.</param>
    /// <returns>True if there are new files, otherwise false.</returns>
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
