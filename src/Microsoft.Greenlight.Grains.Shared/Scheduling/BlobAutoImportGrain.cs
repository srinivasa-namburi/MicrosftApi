using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.Helpers;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling
{
    [Reentrant]
    public class BlobAutoImportGrain : Grain, IBlobAutoImportGrain
    {
        private readonly ILogger<BlobAutoImportGrain> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IServiceProvider _sp;
        private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;

        private const int DefaultDelayMs = 60000; // 1 minute - Orleans Reminder recommended minimum
        private const int DelayAfterImportMs = 120000; // 2 minutes

        public BlobAutoImportGrain(
            ILogger<BlobAutoImportGrain> logger,
            IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot,
            [FromKeyedServices("blob-docing")] BlobServiceClient blobServiceClient,
            IServiceProvider sp)
        {
            _logger = logger;
            _optionsSnapshot = optionsSnapshot;
            _blobServiceClient = blobServiceClient;
            _sp = sp;
        }

        public async Task ExecuteAsync()
        {
            if (_optionsSnapshot.Value.GreenlightServices.DocumentIngestion.ScheduledIngestion == false)
            {
                _logger.LogWarning("Scheduled ingestion is disabled in configuration. Skipping execution.");
                return;
            }

            bool filesFound = false;

            var documentProcessInfoService = _sp.GetRequiredService<IDocumentProcessInfoService>();
            var documentLibraryInfoService = _sp.GetRequiredService<IDocumentLibraryInfoService>();

            _logger.LogDebug("Blob auto-import job triggered at {time}", DateTimeOffset.Now);

            // Process document processes and libraries
            filesFound |= await ProcessSourcesForIngestion(
                "Document Process", 
                async () => await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync(),
                source => source.ShortName,
                source => source.BlobStorageContainerName, 
                source => source.BlobStorageAutoImportFolderName,
                DocumentLibraryType.PrimaryDocumentProcessLibrary);
                
            filesFound |= await ProcessSourcesForIngestion(
                "Document Library", 
                async () => await documentLibraryInfoService.GetAllDocumentLibrariesAsync(),
                source => source.ShortName,
                source => source.BlobStorageContainerName, 
                source => source.BlobStorageAutoImportFolderName,
                DocumentLibraryType.AdditionalDocumentLibrary);

            // Update the reminder with a new delay based on whether files were found
            int newDelayMs = filesFound ? DelayAfterImportMs : DefaultDelayMs;

            _logger.LogDebug("Updating BlobAutoImport reminder with delay: {DelayMs}ms (filesFound: {FilesFound})",
                newDelayMs, filesFound);

            // Get the scheduler orchestration grain and update the reminder
            var schedulerGrain = GrainFactory.GetGrain<ISchedulerOrchestrationGrain>("Scheduler");
            try
            {
                await schedulerGrain.UpdateReminderAsync("BlobAutoImport", TimeSpan.FromMilliseconds(newDelayMs));
            }
            catch (Exception ex)
            {
                // Do nothing - updating the reminder is not critical
            }

        }

        /// <summary>
        /// Generic method to process any source type for document ingestion
        /// </summary>
        /// <typeparam name="TSource">Type of source (DocumentProcessInfo or DocumentLibraryInfo)</typeparam>
        /// <param name="sourceTypeName">Name of the source type for logging</param>
        /// <param name="getSources">Function to get the list of sources</param>
        /// <param name="getShortName">Function to get the short name from a source</param>
        /// <param name="getContainer">Function to get the container name from a source</param>
        /// <param name="getFolder">Function to get the folder name from a source</param>
        /// <param name="libraryType">The document library type to use</param>
        /// <returns>True if files were found in any source, otherwise false</returns>
        private async Task<bool> ProcessSourcesForIngestion<TSource>(
            string sourceTypeName,
            Func<Task<List<TSource>>> getSources,
            Func<TSource, string> getShortName,
            Func<TSource, string> getContainer,
            Func<TSource, string> getFolder,
            DocumentLibraryType libraryType)
        {
            bool filesFound = false;
            
            var sources = await getSources();
            if (sources.Count == 0)
            {
                _logger.LogWarning("No {SourceType}s exist.", sourceTypeName);
                return false;
            }

            foreach (var source in sources)
            {
                var shortName = getShortName(source);
                var container = getContainer(source);
                var folder = getFolder(source);

                if (string.IsNullOrEmpty(container) || string.IsNullOrEmpty(folder))
                {
                    _logger.LogWarning(
                        "Skipping {SourceType} {SourceName} as it has no auto-import folder or container configured",
                        sourceTypeName.ToLowerInvariant(), shortName);
                    continue;
                }

                if (NewFilesInContainerPath(container, folder))
                {
                    filesFound = true;
                    _logger.LogInformation(
                        "New files found for {SourceType} {SourceName}",
                        sourceTypeName.ToLowerInvariant(), shortName);
                        
                    // Use deterministic orchestration ID for container/folder
                    var orchestrationId = IngestionOrchestrationIdHelper.GenerateOrchestrationId(container, folder);
                    var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(orchestrationId);

                    // Avoid overlapping runs for the same orchestration
                    if (await orchestrationGrain.IsRunningAsync())
                    {
                        _logger.LogInformation("Ingestion already running for {Container}/{Folder} (or has pending work). Skipping start.", container, folder);
                        continue;
                    }

                    _ = orchestrationGrain.StartIngestionAsync(
                        shortName,
                        libraryType,
                        container,
                        folder);
                }
            }
            
            return filesFound;
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
