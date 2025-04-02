using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Quartz;

namespace Microsoft.Greenlight.Worker.Scheduler.Jobs
{
    /// <summary>
    /// Quartz job that handles scheduled blob auto-import tasks, with dynamic rescheduling.
    /// </summary>
    public class ScheduledBlobAutoImportJob : IJob
    {
        private readonly ILogger<ScheduledBlobAutoImportJob> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IServiceProvider _sp;
        private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;

        /// <summary>
        /// Constructs a new instance of the <see cref="ScheduledBlobAutoImportJob"/> class.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="optionsSnapshot"></param>
        /// <param name="blobServiceClient"></param>
        /// <param name="sp"></param>
        /// <param name="documentLibraryInfoService"></param>
        public ScheduledBlobAutoImportJob(
            ILogger<ScheduledBlobAutoImportJob> logger,
            IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot,
            BlobServiceClient blobServiceClient,
            IServiceProvider sp,
            IDocumentLibraryInfoService documentLibraryInfoService)
        {
            _logger = logger;
            _optionsSnapshot = optionsSnapshot;
            _blobServiceClient = blobServiceClient;
            _sp = sp;

        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            var documentProcessInfoService = _sp.GetRequiredService<IDocumentProcessInfoService>();
            var documentLibraryInfoService = _sp.GetRequiredService<IDocumentLibraryInfoService>();

            // Check whether scheduled ingestion is enabled.
            if (_optionsSnapshot.Value.GreenlightServices.DocumentIngestion.ScheduledIngestion == false)
            {
                _logger.LogWarning("ScheduledBlobAutoImportJob: Scheduled ingestion is disabled in configuration. Skipping execution.");
                return;
            }

            using var scope = _sp.CreateScope();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            _logger.LogDebug("ScheduledBlobAutoImportJob triggered at {time}", DateTimeOffset.Now);

            // Process document processes and libraries. Each method returns a delay (in milliseconds) that may
            // be modified based on their processing.
            int delayProcess = await ProcessBlobsForDocumentProcesses(publishEndpoint, documentProcessInfoService, context.CancellationToken);
            int delayLibrary = await ProcessBlobsForDocumentLibraries(publishEndpoint, documentLibraryInfoService, context.CancellationToken);

            // Calculate the desired delay for the next execution—here we choose the maximum of both.
            int newDelayMilliseconds = Math.Max(delayProcess, delayLibrary);
            _logger.LogDebug("Rescheduling next execution in {delay} ms", newDelayMilliseconds);

            // Build a new trigger dynamically and reschedule the job with the new delay.
            ITrigger newTrigger = TriggerBuilder.Create()
                .WithIdentity(context.Trigger.Key) // re-use the same trigger key
                .StartAt(DateTimeOffset.Now.AddMilliseconds(newDelayMilliseconds))
                .Build();

            await context.Scheduler.RescheduleJob(context.Trigger.Key, newTrigger);
        }

        private async Task<int> ProcessBlobsForDocumentProcesses(
            IPublishEndpoint publishEndpoint, 
            IDocumentProcessInfoService documentProcessInfoService, 
            CancellationToken stoppingToken)
        {
            int defaultDelayMs = Convert.ToInt32(TimeSpan.FromSeconds(30).TotalMilliseconds);
            int delayAfterImportMs = Convert.ToInt32(TimeSpan.FromMinutes(2).TotalMilliseconds);
            int delayAfterNoneFoundMs = Convert.ToInt32(TimeSpan.FromMinutes(5).TotalMilliseconds);

            var documentProcesses = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
            if (documentProcesses.Count == 0)
            {
                _logger.LogWarning("ScheduledBlobAutoImportJob: No Document Processes exist.");
                return delayAfterNoneFoundMs;
            }

            int computedDelay = defaultDelayMs;

            foreach (var documentProcess in documentProcesses)
            {
                var container = documentProcess.BlobStorageContainerName;
                var folder = documentProcess.BlobStorageAutoImportFolderName;

                if (string.IsNullOrEmpty(container) || string.IsNullOrEmpty(folder))
                {
                    _logger.LogWarning(
                        "ScheduledBlobAutoImportJob: Skipping document process {documentProcessName} as it has no auto-import folder or container configured",
                        documentProcess.ShortName);
                    continue;
                }

                if (NewFilesInContainerPath(container, folder))
                {
                    computedDelay = delayAfterImportMs;
                    _logger.LogInformation(
                        "ScheduledBlobAutoImportJob: New files found for document process {documentProcessName}",
                        documentProcess.ShortName);
                    await publishEndpoint.Publish(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
                    {
                        BlobContainerName = container,
                        FolderPath = folder,
                        DocumentLibraryShortName = documentProcess.ShortName,
                        DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary
                    }, stoppingToken);
                }
            }

            return computedDelay;
        }

        private async Task<int> ProcessBlobsForDocumentLibraries(IPublishEndpoint publishEndpoint,
            IDocumentLibraryInfoService documentLibraryInfoService, CancellationToken stoppingToken)
        {
            int defaultDelayMs = Convert.ToInt32(TimeSpan.FromSeconds(30).TotalMilliseconds);
            int delayAfterImportMs = Convert.ToInt32(TimeSpan.FromMinutes(2).TotalMilliseconds);
            int delayAfterNoneFoundMs = Convert.ToInt32(TimeSpan.FromMinutes(5).TotalMilliseconds);

            var documentLibraries = await documentLibraryInfoService.GetAllDocumentLibrariesAsync();
            if (documentLibraries.Count == 0)
            {
                _logger.LogWarning("ScheduledBlobAutoImportJob: No Document Libraries exist.");
                return delayAfterNoneFoundMs;
            }

            int computedDelay = defaultDelayMs;

            foreach (var documentLibrary in documentLibraries)
            {
                var container = documentLibrary.BlobStorageContainerName;
                var folder = documentLibrary.BlobStorageAutoImportFolderName;

                if (string.IsNullOrEmpty(container) || string.IsNullOrEmpty(folder))
                {
                    _logger.LogWarning(
                        "ScheduledBlobAutoImportJob: Skipping document library {documentLibraryName} as it has no auto-import folder or container configured",
                        documentLibrary.ShortName);
                    continue;
                }

                if (NewFilesInContainerPath(container, folder))
                {
                    computedDelay = delayAfterImportMs;
                    _logger.LogInformation(
                        "ScheduledBlobAutoImportJob: New files found for document library {documentLibraryName}",
                        documentLibrary.ShortName);
                    await publishEndpoint.Publish(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
                    {
                        BlobContainerName = container,
                        FolderPath = folder,
                        DocumentLibraryShortName = documentLibrary.ShortName,
                        DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary
                    }, stoppingToken);
                }
            }

            return computedDelay;
        }

        private bool NewFilesInContainerPath(string containerName, string folderPath)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            // If container does not exist, create it.
            if (!containerClient.Exists())
            {
                containerClient.CreateIfNotExists();
            }

            return containerClient.GetBlobs(prefix: folderPath).Any();
        }
    }
}
