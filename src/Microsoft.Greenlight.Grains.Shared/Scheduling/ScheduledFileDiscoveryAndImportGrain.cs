// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.Helpers;
using Orleans.Concurrency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

[Reentrant]
public class ScheduledFileDiscoveryAndImportGrain : Grain, IScheduledFileDiscoveryAndImportGrain
{
    private readonly ILogger<ScheduledFileDiscoveryAndImportGrain> _logger;
    private readonly IServiceProvider _sp;
    private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;

    private const int DefaultDelayMs = 60000; // 1 minute - Orleans Reminder recommended minimum
    private const int DelayAfterImportMs = 120000; // 2 minutes

    public ScheduledFileDiscoveryAndImportGrain(
        ILogger<ScheduledFileDiscoveryAndImportGrain> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot,
        IServiceProvider sp)
    {
        _logger = logger;
        _optionsSnapshot = optionsSnapshot;
        _sp = sp;
    }

    public async Task ExecuteAsync()
    {
        if (_optionsSnapshot.Value.GreenlightServices.DocumentIngestion.ScheduledIngestion == false)
        {
            _logger.LogWarning("Scheduled ingestion is disabled in configuration. Skipping execution.");
            return;
        }

        // If the heavy Vector Store ID Fix job is enabled and currently running, skip this cycle
        if (_optionsSnapshot.Value.GreenlightServices.Global.EnableVectorStoreIdFixJob)
        {
            var scheduler = GrainFactory.GetGrain<ISchedulerOrchestrationGrain>("Scheduler");
            bool isFixRunning = false;
            try
            {
                isFixRunning = await scheduler.IsVectorStoreIdFixRunningAsync();
            }
            catch
            {
                // Swallow exceptions; if we can't determine, proceed
            }

            if (isFixRunning)
            {
                _logger.LogInformation("Skipping ScheduledFileDiscoveryAndImportGrain execution because Vector Store ID Fix job is running.");
                return;
            }
        }

        bool filesFound = false;

        var documentProcessInfoService = _sp.GetRequiredService<IDocumentProcessInfoService>();
        var documentLibraryInfoService = _sp.GetRequiredService<IDocumentLibraryInfoService>();
        var fileStorageServiceFactory = _sp.GetRequiredService<IFileStorageServiceFactory>();

        _logger.LogDebug("File discovery and import job triggered at {time}", DateTimeOffset.Now);

        // Collect all document processes and libraries with their file storage sources
        var documentProcesses = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        var documentLibraries = await documentLibraryInfoService.GetAllDocumentLibrariesAsync();

        // Group by FileStorageSource to avoid duplicate processing of large repositories
        var fileStorageSourceGroups = new Dictionary<Guid, List<(string shortName, Guid id, DocumentLibraryType type, bool isDocumentLibrary)>>();

        // Add document processes
        foreach (var dp in documentProcesses)
        {
            var fileStorageServices = await fileStorageServiceFactory.GetServicesForDocumentProcessOrLibraryAsync(dp.ShortName, isDocumentLibrary: false);
            foreach (var service in fileStorageServices)
            {
                if (!fileStorageSourceGroups.ContainsKey(service.SourceId))
                {
                    fileStorageSourceGroups[service.SourceId] = new List<(string, Guid, DocumentLibraryType, bool)>();
                }
                fileStorageSourceGroups[service.SourceId].Add((dp.ShortName, dp.Id, DocumentLibraryType.PrimaryDocumentProcessLibrary, false));
            }
        }

        // Add document libraries
        foreach (var dl in documentLibraries)
        {
            var fileStorageServices = await fileStorageServiceFactory.GetServicesForDocumentProcessOrLibraryAsync(dl.ShortName, isDocumentLibrary: true);
            foreach (var service in fileStorageServices)
            {
                if (!fileStorageSourceGroups.ContainsKey(service.SourceId))
                {
                    fileStorageSourceGroups[service.SourceId] = new List<(string, Guid, DocumentLibraryType, bool)>();
                }
                fileStorageSourceGroups[service.SourceId].Add((dl.ShortName, dl.Id, DocumentLibraryType.AdditionalDocumentLibrary, true));
            }
        }

        // Process each FileStorageSource once, handling all DL/DPs that use it
        foreach (var (sourceId, dlDpList) in fileStorageSourceGroups)
        {
            try
            {
                var firstService = await fileStorageServiceFactory.GetServiceBySourceIdAsync(sourceId);
                if (firstService == null)
                {
                    _logger.LogWarning("FileStorageSource {SourceId} not found, skipping", sourceId);
                    continue;
                }

                filesFound |= await ProcessFileStorageSourceForIngestion(firstService, dlDpList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing FileStorageSource {SourceId}", sourceId);
            }
        }

        // Update the reminder with a new delay based on whether files were found
        int newDelayMs = filesFound ? DelayAfterImportMs : DefaultDelayMs;

        _logger.LogDebug("Updating ScheduledFileDiscoveryAndImport reminder with delay: {DelayMs}ms (filesFound: {FilesFound})",
            newDelayMs, filesFound);

        // Get the scheduler orchestration grain and update the reminder
        var schedulerGrain = GrainFactory.GetGrain<ISchedulerOrchestrationGrain>("Scheduler");
        try
        {
            await schedulerGrain.UpdateReminderAsync("ScheduledFileDiscoveryAndImport", TimeSpan.FromMilliseconds(newDelayMs));
        }
        catch (Exception ex)
        {
            // Do nothing - updating the reminder is not critical
            _logger.LogDebug(ex, "Failed to update reminder delay, continuing");
        }
    }

    /// <summary>
    /// Processes a FileStorageSource for ingestion, handling all DL/DPs that use it.
    /// This approach is efficient for large repositories as it discovers files only once per source.
    /// </summary>
    /// <param name="fileStorageService">The file storage service for the source.</param>
    /// <param name="dlDpList">List of document libraries/processes that use this source.</param>
    /// <returns>True if files were found, otherwise false.</returns>
    private async Task<bool> ProcessFileStorageSourceForIngestion(
        IFileStorageService fileStorageService,
        List<(string shortName, Guid id, DocumentLibraryType type, bool isDocumentLibrary)> dlDpList)
    {
        var sourceName = $"FileStorageSource:{fileStorageService.SourceId}";
        
        try
        {
            // Check if this FileStorageSource has new files
            if (!await HasNewFilesAsync(fileStorageService, sourceName))
            {
                return false;
            }

            _logger.LogInformation("New files found for {SourceName} used by {DlDpCount} DL/DPs", 
                sourceName, dlDpList.Count);

            // Use FileStorageSource-centric orchestration ID
            var orchestrationId = IngestionOrchestrationIdHelper.GenerateOrchestrationIdForFileStorageSource(fileStorageService.SourceId);
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(orchestrationId);

            // Avoid overlapping runs for the same orchestration
            if (await orchestrationGrain.IsRunningAsync())
            {
                _logger.LogInformation("Ingestion already running for FileStorageSource {SourceId}. Skipping start.", fileStorageService.SourceId);
                return true; // Files were found, even though we didn't start processing
            }

            // Determine the folder to scan based on move vs acknowledge-only
            var folderToScan = fileStorageService.ShouldMoveFiles
                ? fileStorageService.DefaultAutoImportFolder
                : string.Empty; // root

            // Start orchestration with the FileStorageSource and list of DL/DPs
            _ = orchestrationGrain.StartIngestionAsync(
                fileStorageService.SourceId,
                dlDpList,
                folderToScan);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing FileStorageSource {SourceId}", fileStorageService.SourceId);
            return false;
        }
    }


    /// <summary>
    /// Checks if a file storage service has new files, respecting move vs. acknowledge-only modes
    /// and filtering against acknowledgment records by hash/date.
    /// For shared FileStorageSources, checks both new files and existing acknowledged files needing processing.
    /// </summary>
    /// <param name="fileStorageService">The file storage service to check.</param>
    /// <param name="sourceName">Name of the source for logging.</param>
    /// <returns>True if new files are found, false otherwise.</returns>
    private async Task<bool> HasNewFilesAsync(IFileStorageService fileStorageService, string sourceName)
    {
        try
        {
            await using var db = await _sp.GetRequiredService<IDbContextFactory<DocGenerationDbContext>>().CreateDbContextAsync();

            // First, check for genuinely new files based on the storage mode
            bool hasNewFiles = false;
            
            if (fileStorageService.ShouldMoveFiles)
            {
                // Move mode: check auto-import folder for new files
                var autoImportFiles = await fileStorageService.DiscoverFilesAsync(fileStorageService.DefaultAutoImportFolder);
                var newFileCount = autoImportFiles.Count();
                if (newFileCount > 0)
                {
                    _logger.LogDebug("Found {FileCount} new files in auto-import folder for {SourceName} using {ProviderType}",
                        newFileCount, sourceName, fileStorageService.ProviderType);
                    hasNewFiles = true;
                }
            }
            else
            {
                // Acknowledge-only mode: check root folder against acknowledgment records
                var rootFiles = await fileStorageService.DiscoverFilesAsync(string.Empty);
                var acks = await db.FileAcknowledgmentRecords
                    .Where(a => a.FileStorageSourceId == fileStorageService.SourceId)
                    .ToDictionaryAsync(a => a.RelativeFilePath, a => new { a.FileHash, a.AcknowledgedDate });

                foreach (var file in rootFiles)
                {
                    if (!acks.TryGetValue(file.RelativeFilePath, out var ack))
                    {
                        _logger.LogDebug("New file discovered (no ack) for {SourceName}: {Path}", sourceName, file.RelativeFilePath);
                        hasNewFiles = true;
                        break;
                    }

                    if (!string.IsNullOrEmpty(file.ContentHash) && !string.Equals(file.ContentHash, ack.FileHash, StringComparison.Ordinal))
                    {
                        _logger.LogDebug("File changed (hash diff) for {SourceName}: {Path}", sourceName, file.RelativeFilePath);
                        hasNewFiles = true;
                        break;
                    }

                    if (file.LastModified.ToUniversalTime() > ack.AcknowledgedDate.ToUniversalTime())
                    {
                        string? liveHash = file.ContentHash;
                        if (string.IsNullOrEmpty(liveHash))
                        {
                            try { liveHash = await fileStorageService.GetFileHashAsync(file.RelativeFilePath); }
                            catch { /* Non-fatal; fall through */ }
                        }

                        if (string.IsNullOrEmpty(liveHash) || !string.Equals(liveHash, ack.FileHash, StringComparison.Ordinal))
                        {
                            _logger.LogDebug("File changed (time/hash) for {SourceName}: {Path}", sourceName, file.RelativeFilePath);
                            hasNewFiles = true;
                            break;
                        }
                    }
                }
            }

            // Additional check: For shared FileStorageSources, see if acknowledged files need processing by other DPs/DLs
            // This handles the case where one DP/DL processed files but others sharing the same source haven't
            if (!hasNewFiles)
            {
                // Get all DPs/DLs that use this FileStorageSource
                var documentProcessAssociations = await db.DocumentProcessFileStorageSources
                    .Include(dpfs => dpfs.DocumentProcess)
                    .Where(dpfs => dpfs.FileStorageSourceId == fileStorageService.SourceId && dpfs.IsActive)
                    .Select(dpfs => new { dpfs.DocumentProcess.ShortName, Type = DocumentLibraryType.PrimaryDocumentProcessLibrary })
                    .ToListAsync();

                var documentLibraryAssociations = await db.DocumentLibraryFileStorageSources
                    .Include(dlfs => dlfs.DocumentLibrary)
                    .Where(dlfs => dlfs.FileStorageSourceId == fileStorageService.SourceId && dlfs.IsActive)
                    .Select(dlfs => new { dlfs.DocumentLibrary.ShortName, Type = DocumentLibraryType.AdditionalDocumentLibrary })
                    .ToListAsync();

                var allAssociations = documentProcessAssociations.Cast<object>()
                    .Concat(documentLibraryAssociations.Cast<object>())
                    .ToList();

                // If this source is shared by multiple DPs/DLs, check for missing IngestedDocuments
                if (allAssociations.Count > 1)
                {
                    var acknowledgmentRecords = await db.FileAcknowledgmentRecords
                        .Where(far => far.FileStorageSourceId == fileStorageService.SourceId)
                        .ToListAsync();

                    foreach (var ackRecord in acknowledgmentRecords)
                    {
                        // Check if all DPs/DLs that use this source have IngestedDocuments for this file
                        var existingIngestedDocs = await db.IngestedDocumentFileAcknowledgments
                            .Include(idfa => idfa.IngestedDocument)
                            .Where(idfa => idfa.FileAcknowledgmentRecordId == ackRecord.Id)
                            .Select(idfa => new { 
                                idfa.IngestedDocument.DocumentLibraryOrProcessName, 
                                idfa.IngestedDocument.DocumentLibraryType 
                            })
                            .ToListAsync();

                        // Check if any DP/DL is missing an IngestedDocument for this acknowledged file
                        bool missingForSomeAssociation = false;
                        
                        foreach (var processAssoc in documentProcessAssociations)
                        {
                            if (!existingIngestedDocs.Any(doc => doc.DocumentLibraryOrProcessName == processAssoc.ShortName && 
                                                                 doc.DocumentLibraryType == processAssoc.Type))
                            {
                                missingForSomeAssociation = true;
                                break;
                            }
                        }
                        
                        if (!missingForSomeAssociation)
                        {
                            foreach (var libraryAssoc in documentLibraryAssociations)
                            {
                                if (!existingIngestedDocs.Any(doc => doc.DocumentLibraryOrProcessName == libraryAssoc.ShortName && 
                                                                     doc.DocumentLibraryType == libraryAssoc.Type))
                                {
                                    missingForSomeAssociation = true;
                                    break;
                                }
                            }
                        }

                        if (missingForSomeAssociation)
                        {
                            _logger.LogDebug("Found acknowledged file that needs processing for additional DPs/DLs: {SourceName} - {FilePath}", 
                                sourceName, ackRecord.RelativeFilePath);
                            hasNewFiles = true;
                            break;
                        }
                    }
                }
            }

            return hasNewFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering files for {SourceName} using {ProviderType}",
                sourceName, fileStorageService.ProviderType);
            return false;
        }
    }

    /// <summary>
    /// Tries to get the blob storage container name from a source object using reflection for backward compatibility.
    /// </summary>
    /// <param name="source">The source object (DocumentProcessInfo or DocumentLibraryInfo).</param>
    /// <returns>The container name if found, null otherwise.</returns>
    private static string? GetContainerFromSource(object source)
    {
        var containerProperty = source.GetType().GetProperty("BlobStorageContainerName");
        return containerProperty?.GetValue(source) as string;
    }

    /// <summary>
    /// Tries to get the blob storage auto-import folder name from a source object using reflection for backward compatibility.
    /// </summary>
    /// <param name="source">The source object (DocumentProcessInfo or DocumentLibraryInfo).</param>
    /// <returns>The folder name if found, null otherwise.</returns>
    private static string? GetFolderFromSource(object source)
    {
        var folderProperty = source.GetType().GetProperty("BlobStorageAutoImportFolderName");
        return folderProperty?.GetValue(source) as string;
    }
}
