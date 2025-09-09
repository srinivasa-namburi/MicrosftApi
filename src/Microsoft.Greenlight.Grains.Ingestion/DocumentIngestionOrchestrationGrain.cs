// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.State;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;
using Orleans.Concurrency;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.Models;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Grains.Ingestion;

[Reentrant]
public class DocumentIngestionOrchestrationGrain : Grain, IDocumentIngestionOrchestrationGrain
{
    private readonly ILogger<DocumentIngestionOrchestrationGrain> _logger;
    private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly IPersistentState<DocumentIngestionState> _state;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly ISemanticKernelVectorStoreProvider _vectorStoreProvider;
    private readonly IFileStorageServiceFactory _fileStorageServiceFactory;

    // In-memory counter for active child processes
    private int _activeChildCount = 0;
    // In-memory RunId for the current ingestion run
    private Guid? _currentRunId = null;
    // In-memory activity flag for this orchestration activation
    private volatile bool _isActive = false;

    public DocumentIngestionOrchestrationGrain(
        [PersistentState("docIngestOrchestration")]
        IPersistentState<DocumentIngestionState> state,
        ILogger<DocumentIngestionOrchestrationGrain> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        AzureFileHelper azureFileHelper,
        ISemanticKernelVectorStoreProvider vectorStoreProvider,
        IFileStorageServiceFactory fileStorageServiceFactory)
    {
        _state = state;
        _logger = logger;
        _optionsSnapshot = optionsSnapshot;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
        _dbContextFactory = dbContextFactory;
        _azureFileHelper = azureFileHelper;
        _vectorStoreProvider = vectorStoreProvider;
        _fileStorageServiceFactory = fileStorageServiceFactory;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_state.State.Id))
        {
            _state.State.Id = this.GetPrimaryKeyString();
            _state.State.Status = IngestionOrchestrationState.NotStarted;
            _state.State.Errors = [];
            await SafeWriteStateAsync();
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public Task<bool> IsRunningAsync()
    {
        // Only rely on the in-memory flag for the active orchestration in this activation
        return Task.FromResult(_isActive);
    }

    public async Task DeactivateAsync()
    {
        try
        {
            _logger.LogInformation("Deactivation requested for ingestion orchestration {OrchestrationId}", this.GetPrimaryKeyString());
            _isActive = false;
            DeactivateOnIdle();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error requesting deactivation for {OrchestrationId}", this.GetPrimaryKeyString());
        }
        await Task.CompletedTask;
    }

    public async Task StartIngestionAsync(
        Guid fileStorageSourceId,
        List<(string shortName, Guid id, DocumentLibraryType type, bool isDocumentLibrary)> dlDpList,
        string folderPath)
    {
        // Back off if this orchestration activation is already running
        if (_isActive)
        {
            _logger.LogInformation("Orchestration {OrchestrationId} is already active. Skipping start.", this.GetPrimaryKeyString());
            return;
        }

        // Mark orchestration as active for this activation as we are starting now
        _isActive = true;

        // Generate a new RunId for this ingestion run
        _currentRunId = Guid.NewGuid();
        var runId = _currentRunId.Value;
        var orchestrationId = this.GetPrimaryKeyString();

        _logger.LogInformation("Starting FileStorageSource-centric ingestion for source {SourceId} serving {DlDpCount} DL/DPs", 
            fileStorageSourceId, dlDpList.Count);

        try
        {
            // Ensure this FileStorageSource is intended for ingestion
            try
            {
                await using var checkDb = await _dbContextFactory.CreateDbContextAsync();
                var src = await checkDb.FileStorageSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == fileStorageSourceId);
                var isIngestion = src != null && src.StorageSourceDataType == FileStorageSourceDataType.Ingestion;
                if (!isIngestion)
                {
                    // Check multi-category assignment
                    isIngestion = await checkDb.FileStorageSourceCategories
                        .AsNoTracking()
                        .AnyAsync(c => c.FileStorageSourceId == fileStorageSourceId && c.DataType == FileStorageSourceDataType.Ingestion);
                }
                if (!isIngestion)
                {
                    _logger.LogInformation("Skipping ingestion for non-ingestion storage source {SourceId} ({Type})", fileStorageSourceId, src.StorageSourceDataType);
                    _isActive = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to verify StorageSourceDataType for {SourceId}; proceeding cautiously.", fileStorageSourceId);
            }

            // Get the FileStorageService for this source
            var fileStorageService = await _fileStorageServiceFactory.GetServiceBySourceIdAsync(fileStorageSourceId);
            
            if (fileStorageService == null)
            {
                _logger.LogError("FileStorageSource {SourceId} not found", fileStorageSourceId);
                _state.State.Status = IngestionOrchestrationState.Failed;
                _state.State.Errors.Add($"FileStorageSource {fileStorageSourceId} not found");
                await SafeWriteStateAsync();
                _isActive = false;
                return;
            }

            // Discover files once from the FileStorageSource
            var files = await fileStorageService.DiscoverFilesAsync(folderPath);
            
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            int newFiles = 0;
            int resetFiles = 0;
            int skippedAlreadyProcessed = 0;

            // Register discovered files using the service (creates/updates FAKs without movement)
            var hasAnyNewOrChangedFiles = false;
            var fileAcknowledgmentIds = new Dictionary<string, Guid>();
            
            foreach (var file in files)
            {
                var relativePath = file.RelativeFilePath;
                var fileHash = file.ContentHash;

                // Check if we've seen this file before
                var existingRecord = await db.FileAcknowledgmentRecords
                    .FirstOrDefaultAsync(r => r.FileStorageSourceId == fileStorageSourceId && 
                                             r.RelativeFilePath == relativePath);

                // If file is new or hash changed, register the discovery
                if (existingRecord == null || !string.Equals(existingRecord.FileHash, fileHash, StringComparison.Ordinal))
                {
                    // Register discovery through the service (creates/updates FAK without movement)
                    var acknowledgmentId = await fileStorageService.RegisterFileDiscoveryAsync(relativePath, fileHash);
                    fileAcknowledgmentIds[relativePath] = acknowledgmentId;
                    hasAnyNewOrChangedFiles = true;
                    
                    _logger.LogInformation("Registered discovery for new/changed file {RelativePath} with hash {Hash}", 
                        relativePath, fileHash);
                }
                else
                {
                    // File unchanged, use existing acknowledgment ID
                    fileAcknowledgmentIds[relativePath] = existingRecord.Id;
                }
            }

            // Process each discovered file for each DL/DP
            foreach (var file in files)
            {
                var relativePath = file.RelativeFilePath;
                var fullPath = fileStorageService.GetFullPath(relativePath);
                var fileHash = file.ContentHash;
                
                // Get the acknowledgment ID for this file (was set during discovery registration above)
                var acknowledgmentId = fileAcknowledgmentIds[relativePath];

                // For files that are unchanged (already acknowledged), we may still need to
                // create per-view IngestedDocuments for newly associated DL/DPs.
                // We only skip entirely if all DL/DP views already exist.
                bool fileAlreadyProcessedInAllViews = true;

                // Create or update IngestedDocument for each DL/DP that uses this FileStorageSource
                foreach (var (shortName, id, libraryType, isDocumentLibrary) in dlDpList)
                {
                    // Check if IngestedDocument already exists for this DL/DP + file combination
                    var existingDoc = await db.IngestedDocuments
                        .FirstOrDefaultAsync(d => d.DocumentLibraryOrProcessName == shortName &&
                                                 d.DocumentLibraryType == libraryType &&
                                                 d.FileName == System.IO.Path.GetFileName(relativePath) &&
                                                 d.Container == $"FileStorageSource:{fileStorageSourceId}");

                    if (existingDoc == null)
                    {
                        fileAlreadyProcessedInAllViews = false;

                        var ingestedDocument = new IngestedDocument
                        {
                            Id = Guid.NewGuid(),
                            FileName = System.IO.Path.GetFileName(relativePath),
                            Container = $"FileStorageSource:{fileStorageSourceId}",
                            FolderPath = folderPath,
                            OrchestrationId = orchestrationId,
                            RunId = runId,
                        // For FileStorageSource, mark as discovered for this consumer to route through copy stage queue
                            IngestionState = IngestionState.DiscoveredForConsumer,
                            IngestedDate = DateTime.UtcNow,
                            OriginalDocumentUrl = fullPath, // Raw storage path
                            FinalBlobUrl = null, // Will be set after copy/acknowledgment
                            DocumentLibraryType = libraryType,
                            DocumentLibraryOrProcessName = shortName,
                            FileHash = fileHash
                        };

                        db.IngestedDocuments.Add(ingestedDocument);

                        // Link to the FileAcknowledgmentRecord that was created/updated during discovery
                        var acknowledgmentLink = new IngestedDocumentFileAcknowledgment
                        {
                            Id = Guid.NewGuid(),
                            IngestedDocumentId = ingestedDocument.Id,
                            FileAcknowledgmentRecordId = acknowledgmentId
                        };
                        db.IngestedDocumentFileAcknowledgments.Add(acknowledgmentLink);

                        newFiles++;
                    }
                    else if (existingDoc.IngestionState == IngestionState.Failed)
                    {
                        fileAlreadyProcessedInAllViews = false;

                        // Reset failed documents for re-ingestion; route via copy stage queue
                        existingDoc.IngestionState = IngestionState.DiscoveredForConsumer;
                        existingDoc.IngestedDate = DateTime.UtcNow;
                        existingDoc.OriginalDocumentUrl = fullPath;
                        existingDoc.FinalBlobUrl = fullPath; // For FileStorageSource, no file movement
                        existingDoc.RunId = runId;
                        existingDoc.Error = null;
                        existingDoc.FileHash = fileHash;
                        resetFiles++;
                    }
                }

                if (hasAnyNewOrChangedFiles)
                {
                    // If any files were new or changed, we processed them above
                }
                else if (fileAlreadyProcessedInAllViews)
                {
                    // All view documents already exist and are not failed; count as skipped
                    skippedAlreadyProcessed++;
                }
            }

            // Additional check: Look for acknowledged files that need processing by additional DL/DPs
            // This handles cases where some DL/DPs have processed files but others sharing the same source haven't
            if (newFiles == 0 && resetFiles == 0)
            {
                var allAcknowledgmentRecords = await db.FileAcknowledgmentRecords
                    .Where(far => far.FileStorageSourceId == fileStorageSourceId)
                    .ToListAsync();

                foreach (var ackRecord in allAcknowledgmentRecords)
                {
                    // Check if all DL/DPs that use this source have IngestedDocuments for this file
                    var existingIngestedDocs = await db.IngestedDocumentFileAcknowledgments
                        .Include(idfa => idfa.IngestedDocument)
                        .Where(idfa => idfa.FileAcknowledgmentRecordId == ackRecord.Id)
                        .Select(idfa => new { 
                            idfa.IngestedDocument.DocumentLibraryOrProcessName, 
                            idfa.IngestedDocument.DocumentLibraryType 
                        })
                        .ToListAsync();

                    // Check if any DL/DP is missing an IngestedDocument for this acknowledged file
                    foreach (var (shortName, id, libraryType, isDocumentLibrary) in dlDpList)
                    {
                        if (!existingIngestedDocs.Any(doc => doc.DocumentLibraryOrProcessName == shortName && 
                                                             doc.DocumentLibraryType == libraryType))
                        {
                            // This DL/DP is missing an IngestedDocument for this acknowledged file
                            var fileName = System.IO.Path.GetFileName(ackRecord.RelativeFilePath);
                            var ingestedDocument = new IngestedDocument
                            {
                                Id = Guid.NewGuid(),
                                FileName = fileName,
                                Container = $"FileStorageSource:{fileStorageSourceId}",
                                FolderPath = folderPath,
                                OrchestrationId = orchestrationId,
                                RunId = runId,
                                // Mark as DiscoveredForConsumer since file has already been processed/moved by another DL/DP
                                IngestionState = IngestionState.DiscoveredForConsumer,
                                IngestedDate = DateTime.UtcNow,
                                OriginalDocumentUrl = ackRecord.FileStorageSourceInternalUrl,
                                FinalBlobUrl = ackRecord.FileStorageSourceInternalUrl,
                                DocumentLibraryType = libraryType,
                                DocumentLibraryOrProcessName = shortName,
                                FileHash = ackRecord.FileHash
                            };

                            db.IngestedDocuments.Add(ingestedDocument);

                            // Link to the existing FileAcknowledgmentRecord
                            var acknowledgmentLink = new IngestedDocumentFileAcknowledgment
                            {
                                Id = Guid.NewGuid(),
                                IngestedDocumentId = ingestedDocument.Id,
                                FileAcknowledgmentRecordId = ackRecord.Id
                            };
                            db.IngestedDocumentFileAcknowledgments.Add(acknowledgmentLink);

                            newFiles++;
                            
                            _logger.LogInformation("Created IngestedDocument for acknowledged file {FileName} for DL/DP {ShortName} that was missing it",
                                fileName, shortName);
                        }
                    }
                }
            }

            if (newFiles > 0 || resetFiles > 0)
            {
                try
                {
                    await db.SaveChangesAsync();
                    _logger.LogInformation("FileStorageSource-centric ingestion: Added {NewCount} new files and reset {ResetCount} failed files across {DlDpCount} DL/DPs. Skipped {SkippedCount} already processed files.",
                        newFiles, resetFiles, dlDpList.Count, skippedAlreadyProcessed);
                }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && 
                                                 sqlEx.Number == 2601 && // Unique constraint violation
                                                 sqlEx.Message.Contains("IX_IngestedDocuments_DocumentLibraryType_DocumentLibraryOrProcessName_Container_FolderPath_FileName_FileHash"))
                {
                    _logger.LogWarning("Detected orphaned IngestedDocument records causing unique constraint violations. Attempting recovery...");
                    
                    // Attempt recovery by cleaning up orphaned records and retrying
                    var recoveredCount = await CleanupOrphanedIngestedDocumentsAndRetryAsync(db, fileStorageSourceId, dlDpList, folderPath, orchestrationId.ToString(), runId.ToString());
                    
                    if (recoveredCount > 0)
                    {
                        _logger.LogInformation("Recovered from orphaned records: Cleaned up {RecoveredCount} orphans and successfully completed ingestion with {NewCount} new files and {ResetCount} reset files.",
                            recoveredCount, newFiles, resetFiles);
                    }
                    else
                    {
                        _logger.LogWarning("Could not recover from orphaned IngestedDocument records. Continuing without adding conflicting files to avoid blocking ingestion.");
                    }
                }
            }
            else
            {
                _logger.LogInformation("No new or failed files for FileStorageSource {SourceId}. Skipped {SkippedCount} already processed files.",
                    fileStorageSourceId, skippedAlreadyProcessed);
            }

            // Calculate totals across all DL/DPs for this run
            var runDocs = await db.IngestedDocuments.Where(x => x.OrchestrationId == orchestrationId && x.RunId == runId).ToListAsync();
            _state.State.TotalFiles = runDocs.Count;
            _state.State.ProcessedFiles = runDocs.Count(x => x.IngestionState == IngestionState.Complete);
            _state.State.FailedFiles = runDocs.Count(x => x.IngestionState == IngestionState.Failed);
            _state.State.Status = IngestionOrchestrationState.Running;
            await SafeWriteStateAsync();

            // If there's nothing to process, finish gracefully
            if (_state.State.TotalFiles == 0 || runDocs.All(x => x.IngestionState == IngestionState.Complete || x.IngestionState == IngestionState.Failed))
            {
                _state.State.Status = _state.State.FailedFiles > 0 ? IngestionOrchestrationState.Failed : IngestionOrchestrationState.Completed;
                await SafeWriteStateAsync();
                _isActive = false;
                _logger.LogInformation("No pending files for FileStorageSource orchestration {OrchestrationId}. Marking as {Status}", orchestrationId, _state.State.Status);
                return;
            }

            // Start parallel file processing
            await ResumeOrStartIngestionAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FileStorageSource-centric ingestion for source {SourceId}", fileStorageSourceId);
            _state.State.Status = IngestionOrchestrationState.Failed;
            _state.State.Errors.Add($"FileStorageSource ingestion error: {ex.Message}");
            await SafeWriteStateAsync();
            _isActive = false;
        }
    }

    /// <summary>
    /// Diagnostic method to check the status of stuck documents and attempt recovery.
    /// </summary>
    public async Task CheckAndRecoverStuckDocumentsAsync()
    {
        var orchestrationId = this.GetPrimaryKeyString();
        _logger.LogInformation("Checking for stuck documents in orchestration {OrchestrationId}", orchestrationId);

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        // Find documents that have been in Processing state for more than 30 minutes
        var thirtyMinutesAgo = DateTime.UtcNow.AddMinutes(-30);
        var stuckDocuments = await db.IngestedDocuments
            .Where(x => x.OrchestrationId == orchestrationId && 
                        x.IngestionState == IngestionState.Processing &&
                        x.IngestedDate < thirtyMinutesAgo)
            .ToListAsync();

        if (stuckDocuments.Count == 0)
        {
            _logger.LogInformation("No stuck documents found in orchestration {OrchestrationId}", orchestrationId);
            return;
        }

        _logger.LogWarning("Found {StuckCount} documents stuck in Processing state for orchestration {OrchestrationId}", 
            stuckDocuments.Count, orchestrationId);

        foreach (var doc in stuckDocuments)
        {
            try
            {
                _logger.LogInformation("Attempting to recover stuck document {DocumentId} ({FileName})", doc.Id, doc.FileName);
                
                var fileGrain = GrainFactory.GetGrain<IFileIngestionGrain>(doc.Id);
                var isActive = await fileGrain.IsActiveAsync();
                
                if (!isActive)
                {
                    // Document is not actively being processed, attempt to restart it
                    _logger.LogInformation("Restarting inactive stuck document {DocumentId} ({FileName})", doc.Id, doc.FileName);
                    
                    // Reset the state to FileCopied so it will be reprocessed
                    doc.IngestionState = IngestionState.FileCopied;
                    doc.Error = "Recovered from stuck processing state";
                    await db.SaveChangesAsync();
                    
                    // Fire-and-forget restart
                    _ = fileGrain.StartIngestionAsync(doc.Id);
                }
                else
                {
                    _logger.LogInformation("Document {DocumentId} ({FileName}) is still actively being processed", doc.Id, doc.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover stuck document {DocumentId} ({FileName})", doc.Id, doc.FileName);
            }
        }
    }

    public async Task StartIngestionAsync(
        string documentLibraryShortName,
        DocumentLibraryType documentLibraryType,
        string blobContainerName,
        string folderPath)
    {
        // Back off if this orchestration activation is already running
        if (_isActive)
        {
            _logger.LogInformation("Orchestration {OrchestrationId} is already active. Skipping start.", this.GetPrimaryKeyString());
            return;
        }

        // Mark orchestration as active for this activation as we are starting now
        _isActive = true;

        // Generate a new RunId for this ingestion run
        _currentRunId = Guid.NewGuid();
        var runId = _currentRunId.Value;
        var orchestrationId = this.GetPrimaryKeyString();

        // Recalculate state from DB before deciding to skip or start
        await using (var db = await _dbContextFactory.CreateDbContextAsync())
        {
            var potentialFiles = db.IngestedDocuments
                .Where(x => x.OrchestrationId == orchestrationId &&
                            x.IngestionState != IngestionState.Complete &&
                            x.IngestionState != IngestionState.Failed);

            // Move abandoned files to the new RunId (excluding those with active FileIngestionGrain)
            foreach (var file in potentialFiles)
            {
                var fileGrain = GrainFactory.GetGrain<IFileIngestionGrain>(file.Id);
                if (!await fileGrain.IsActiveAsync())
                {
                    file.RunId = runId;
                }
            }
            await db.SaveChangesAsync();

            // Now, only consider files with the current RunId for this run
            var runDocs = db.IngestedDocuments
                .Where(x => x.OrchestrationId == orchestrationId && x.RunId == runId);
            _state.State.TotalFiles = await runDocs.CountAsync();
            _state.State.ProcessedFiles = await runDocs.CountAsync(x => x.IngestionState == IngestionState.Complete);
            _state.State.FailedFiles = await runDocs.CountAsync(x => x.IngestionState == IngestionState.Failed);
            await SafeWriteStateAsync();

            // Only skip if ALL fileGrains for this run are active
            if (_state.State.Status == IngestionOrchestrationState.Running && runDocs.Any())
            {
                bool allActive = true;
                foreach (var file in runDocs)
                {
                    var fileGrain = GrainFactory.GetGrain<IFileIngestionGrain>(file.Id);
                    if (!await fileGrain.IsActiveAsync())
                    {
                        allActive = false;
                        break;
                    }
                }
                if (allActive)
                {
                    _logger.LogInformation("Ingestion already running for {Container}/{Folder}, skipping.", blobContainerName, folderPath);
                    _isActive = false; // ensure we don't stay active if we didn't actually start anything
                    return;
                }
            }
            // If no files left and state is Running, mark as complete/failed
            if (_state.State.Status == IngestionOrchestrationState.Running && !runDocs.Any() && _state.State.TotalFiles > 0)
            {
                _state.State.Status = _state.State.FailedFiles > 0 ? IngestionOrchestrationState.Failed : IngestionOrchestrationState.Completed;
                await SafeWriteStateAsync();
                _logger.LogInformation("[StartIngestionAsync] Orchestration {OrchestrationId} marked as {Status} (no files left)", orchestrationId, _state.State.Status);
                _isActive = false;
                return;
            }
        }

    _state.State.DocumentLibraryShortName = documentLibraryShortName;
    _state.State.DocumentLibraryType = documentLibraryType;
    _state.State.TargetContainerName = string.Empty; // Will be set after validation

        // Validate document library/process exists
        bool isNonBlob = string.Equals(blobContainerName, "__nonblob__", StringComparison.Ordinal);
        if (!isNonBlob)
        {
            if (documentLibraryType == DocumentLibraryType.AdditionalDocumentLibrary)
            {
                var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);
                if (documentLibrary == null)
                {
                    _logger.LogError("Document library {DocumentLibraryName} not found", documentLibraryShortName);
                    _state.State.Status = IngestionOrchestrationState.Failed;
                    _state.State.Errors.Add($"Document library {documentLibraryShortName} not found");
                    await SafeWriteStateAsync();
                    _isActive = false;
                    return;
                }
                // For the new architecture, use blobContainerName parameter (passed by scheduler) as target
                _state.State.TargetContainerName = blobContainerName;
            }
            else // PrimaryDocumentProcessLibrary
            {
                var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryShortName);
                if (documentProcess == null)
                {
                    _logger.LogError("Document process {DocumentProcessName} not found", documentLibraryShortName);
                    _state.State.Status = IngestionOrchestrationState.Failed;
                    _state.State.Errors.Add($"Document process {documentLibraryShortName} not found");
                    await SafeWriteStateAsync();
                    _isActive = false;
                    return;
                }
                // For the new architecture, use blobContainerName parameter (passed by scheduler) as target
                _state.State.TargetContainerName = blobContainerName;
            }
        }

        List<BlobHashInfo> blobHashes = new();
        if (!isNonBlob)
        {
            // Use hashing grain to enumerate and hash blobs in parallel.
            var hashingGrainKey = orchestrationId; // tie hashing to orchestration
            var hashingGrain = GrainFactory.GetGrain<IBlobHashingGrain>(hashingGrainKey);
            if (await hashingGrain.IsActiveAsync())
            {
                _logger.LogInformation("Hashing already active for orchestration {OrchestrationId}. Backing off this cycle.", orchestrationId);
                _isActive = false; // let scheduler retry without blocking
                return; // let the scheduler retry on its next cycle
            }

            blobHashes = await hashingGrain.StartHashingAsync(_state.State.TargetContainerName, folderPath, runId);
        }

        // Enumerate blobs based on the results and add/update IngestedDocument entries
        await using (var db = await _dbContextFactory.CreateDbContextAsync())
        {
            // Fetch all existing documents for this orchestration to perform in-memory lookups or updates.
            var allExistingDocsForOrchestration = await db.IngestedDocuments
                .Where(x => x.OrchestrationId == orchestrationId)
                .ToListAsync();

            int newFiles = 0;
            int resetFiles = 0;
            int skippedAlreadyProcessed = 0;

            if (isNonBlob)
            {
                // For non-blob, entries should already be seeded in DB with this orchestration id. No hashing or discovery here.
                _logger.LogInformation("[StartIngestionAsync] Non-blob orchestration {OrchestrationId}: skipping blob discovery and proceeding to processing queued documents.", orchestrationId);
            }
            else
            {
                foreach (var entry in blobHashes)
            {
                var relativeFileName = entry.RelativeFileName;
                var fullBlobUrl = entry.FullBlobUrl;
                var currentHash = entry.Hash;

                IngestedDocument? existingDoc = null;
                if (!string.IsNullOrEmpty(currentHash))
                {
                    // Try exact match including hash first
                    existingDoc = allExistingDocsForOrchestration.FirstOrDefault(d =>
                        d.FolderPath == folderPath &&
                        d.FileName == relativeFileName &&
                        d.Container == _state.State.TargetContainerName &&
                        d.DocumentLibraryType == documentLibraryType &&
                        d.DocumentLibraryOrProcessName == documentLibraryShortName &&
                        d.FileHash == currentHash);

                    // Backfill hash if we only have a name match without hash stored
                    if (existingDoc == null)
                    {
                        var existingByName = allExistingDocsForOrchestration.FirstOrDefault(d =>
                            d.FolderPath == folderPath &&
                            d.FileName == relativeFileName &&
                            d.Container == _state.State.TargetContainerName &&
                            d.DocumentLibraryType == documentLibraryType &&
                            d.DocumentLibraryOrProcessName == documentLibraryShortName);
                        if (existingByName != null && string.IsNullOrEmpty(existingByName.FileHash))
                        {
                            existingByName.FileHash = currentHash;
                            await db.SaveChangesAsync();
                            existingDoc = existingByName;
                        }
                    }
                }
                else
                {
                    // Fall back to name-only match if we couldn't compute a hash
                    existingDoc = allExistingDocsForOrchestration.FirstOrDefault(d =>
                        d.FolderPath == folderPath &&
                        d.FileName == relativeFileName &&
                        d.Container == _state.State.TargetContainerName &&
                        d.DocumentLibraryType == documentLibraryType &&
                        d.DocumentLibraryOrProcessName == documentLibraryShortName);
                }

                if (existingDoc == null)
                {
                    _logger.LogInformation("New file discovered: Container={Container}, FolderPath={FolderPath}, FileName={RelativeFileName}",
                        _state.State.TargetContainerName, folderPath, relativeFileName);
                    
                    // Create IngestedDocument for this specific DP/DL
                    var ingestedDocument = new IngestedDocument
                    {
                        Id = Guid.NewGuid(),
                        FileName = relativeFileName, // Store relative file name
                        Container = _state.State.TargetContainerName,
                        FolderPath = folderPath, // Store the scanned folder path
                        OrchestrationId = orchestrationId,
                        RunId = runId,
                        IngestionState = IngestionState.Discovered,
                        IngestedDate = DateTime.UtcNow,
                        OriginalDocumentUrl = fullBlobUrl, // Full path for URL
                        DocumentLibraryType = documentLibraryType,
                        DocumentLibraryOrProcessName = documentLibraryShortName,
                        FileHash = currentHash
                    };
                    
                    db.IngestedDocuments.Add(ingestedDocument);

                    // Register file discovery through FileStorageService
                    // Identify the FileStorageSource this orchestration represents
                    Guid? fileStorageSourceId = await GetFileStorageSourceIdAsync(db, documentLibraryType, documentLibraryShortName, _state.State.TargetContainerName);
                    
                    if (fileStorageSourceId.HasValue)
                    {
                        // Get the FileStorageService for this source
                        var fileStorageService = await _fileStorageServiceFactory.GetServiceBySourceIdAsync(fileStorageSourceId.Value);
                        
                        if (fileStorageService != null)
                        {
                            var relativePath = $"{folderPath}/{relativeFileName}";
                            
                            // Register discovery (creates/updates FAK without movement)
                            var acknowledgmentId = await fileStorageService.RegisterFileDiscoveryAsync(relativePath, currentHash);
                            
                            // Create the link between IngestedDocument and FileAcknowledgmentRecord
                            var acknowledgmentLink = new IngestedDocumentFileAcknowledgment
                            {
                                Id = Guid.NewGuid(),
                                IngestedDocumentId = ingestedDocument.Id,
                                FileAcknowledgmentRecordId = acknowledgmentId
                            };
                            db.IngestedDocumentFileAcknowledgments.Add(acknowledgmentLink);
                            
                            _logger.LogInformation("Registered discovery for blob {RelativePath} with hash {Hash}", 
                                relativePath, currentHash);
                        }
                        else
                        {
                            _logger.LogWarning("FileStorageService not found for source {SourceId}, skipping discovery registration", fileStorageSourceId.Value);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No FileStorageSourceId found for {Container}/{FolderPath}, skipping discovery registration", 
                            _state.State.TargetContainerName, folderPath);
                    }
                    
                    newFiles++;
                }
                else // Document already exists in DB for this OrchestrationId, FolderPath, and FileName
                {
                    switch (existingDoc.IngestionState)
                    {
                        case IngestionState.Failed:
                            {
                                _logger.LogInformation("Resetting failed document for re-ingestion: {ExistingDocFileName} in folder {ExistingDocFolderPath}", existingDoc.FileName, existingDoc.FolderPath);
                                existingDoc.IngestionState = IngestionState.Discovered;
                                existingDoc.IngestedDate = DateTime.UtcNow;
                                existingDoc.OriginalDocumentUrl = fullBlobUrl;
                                existingDoc.RunId = runId;
                                existingDoc.Error = null;
                                if (string.IsNullOrEmpty(existingDoc.FileHash) && !string.IsNullOrEmpty(currentHash))
                                {
                                    existingDoc.FileHash = currentHash;
                                }
                                resetFiles++;
                                break;
                            }
                        case IngestionState.Complete:
                            {
                                // If the document is already marked as complete in the DB, check if it exists in the vector store (SK only).
                                // If present only under old id, schedule migration to canonical id.
                                bool scheduledForReindex = false;
                                try
                                {
                                    // Determine if SK Vector Store is used and resolve index/document IDs
                                    string? indexName = existingDoc.VectorStoreIndexName;
                                    string? storedDocId = existingDoc.VectorStoreDocumentId;

                                    if (documentLibraryType == DocumentLibraryType.AdditionalDocumentLibrary)
                                    {
                                        var docLib = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);
                                        if (docLib?.LogicType == DocumentProcessLogicType.SemanticKernelVectorStore)
                                        {
                                            indexName ??= docLib.IndexName;
                                        }
                                        else
                                        {
                                            indexName = null; // Not SK Vector Store - fall through to skip/delete
                                        }
                                    }
                                    else
                                    {
                                        var docProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryShortName);
                                        if (docProcess?.LogicType == DocumentProcessLogicType.SemanticKernelVectorStore)
                                        {
                                            indexName ??= (docProcess.Repositories?.FirstOrDefault() ?? documentLibraryShortName);
                                        }
                                        else
                                        {
                                            indexName = null;
                                        }
                                    }

                                    if (!string.IsNullOrWhiteSpace(indexName))
                                    {
                                        var canonicalId = Base64UrlEncode(SanitizeFileName(existingDoc.FileName));

                                        // Check canonical id first
                                        var canonicalPartitions = await _vectorStoreProvider.GetDocumentPartitionNumbersAsync(indexName!, canonicalId);
                                        if (canonicalPartitions != null && canonicalPartitions.Count > 0)
                                        {
                                            // Already indexed under canonical id: heal DB to canonical
                                            if (!string.Equals(existingDoc.VectorStoreDocumentId, canonicalId, StringComparison.Ordinal))
                                            {
                                                existingDoc.VectorStoreDocumentId = canonicalId;
                                                existingDoc.VectorStoreIndexName = indexName;
                                                existingDoc.IsVectorStoreIndexed = true;
                                                existingDoc.VectorStoreIndexedDate = existingDoc.VectorStoreIndexedDate ?? DateTime.UtcNow;
                                                await db.SaveChangesAsync();
                                                _logger.LogInformation("Healed document {File} to canonical vector id in index {Index}", existingDoc.FileName, indexName);
                                            }
                                        }
                                        else
                                        {
                                            // Not found under canonical; check stored/old id
                                            IReadOnlyList<int> storedPartitions = Array.Empty<int>();
                                            if (!string.IsNullOrWhiteSpace(storedDocId) && !string.Equals(storedDocId, canonicalId, StringComparison.Ordinal))
                                            {
                                                storedPartitions = await _vectorStoreProvider.GetDocumentPartitionNumbersAsync(indexName!, storedDocId);
                                            }

                                            if (storedPartitions != null && storedPartitions.Count > 0)
                                            {
                                                // Found only under old id -> schedule reindex to migrate to canonical id
                                                existingDoc.IsVectorStoreIndexed = false;
                                                existingDoc.VectorStoreIndexedDate = null;
                                                existingDoc.VectorStoreChunkCount = 0;
                                                existingDoc.OriginalDocumentUrl = fullBlobUrl;
                                                existingDoc.RunId = runId;
                                                existingDoc.VectorStoreDocumentId = canonicalId; // set desired target id

                                                // If we already have a copied blob, skip copy step and go straight to processing
                                                existingDoc.IngestionState = string.IsNullOrWhiteSpace(existingDoc.FinalBlobUrl)
                                                    ? IngestionState.Discovered
                                                    : IngestionState.FileCopied;

                                                scheduledForReindex = true;
                                                resetFiles++;
                                                _logger.LogInformation("Scheduled document {File} for id migration to canonical id in index {Index}", existingDoc.FileName, indexName);
                                            }
                                            else
                                            {
                                                // Not present at all -> schedule for reindex
                                                existingDoc.IsVectorStoreIndexed = false;
                                                existingDoc.VectorStoreIndexedDate = null;
                                                existingDoc.VectorStoreChunkCount = 0;
                                                existingDoc.OriginalDocumentUrl = fullBlobUrl;
                                                existingDoc.RunId = runId;
                                                existingDoc.VectorStoreDocumentId = canonicalId; // ensure correct target id

                                                existingDoc.IngestionState = string.IsNullOrWhiteSpace(existingDoc.FinalBlobUrl)
                                                    ? IngestionState.Discovered
                                                    : IngestionState.FileCopied;

                                                scheduledForReindex = true;
                                                resetFiles++;
                                                _logger.LogInformation("Scheduled document {File} for re-indexing (missing from vector store index {Index})", existingDoc.FileName, indexName);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to verify vector store presence for {File}; defaulting to existing behavior", existingDoc.FileName);
                                }

                                if (!scheduledForReindex)
                                {
                                    // Already processed and present, no action required on source in leave-in-place mode
                                    skippedAlreadyProcessed++;
                                }

                                break;
                            }
                        default:
                            _logger.LogDebug("Document {ExistingDocFileName} in folder {ExistingDocFolderPath} already exists with state {IngestionState}. It will be handled by the current run if eligible.",
                                existingDoc.FileName, existingDoc.FolderPath, existingDoc.IngestionState);
                            break;
                    }
                }
                }
            }

            if (newFiles > 0 || resetFiles > 0)
            {
                await db.SaveChangesAsync();
                _logger.LogInformation("Committed DB changes: Added {NewCount} new files and reset {ResetCount} failed/missing-index files for orchestration {OrchestrationId}. Skipped {SkippedCount} already processed files.",
                    newFiles, resetFiles, orchestrationId, skippedAlreadyProcessed);
            }
            else
            {
                _logger.LogInformation("No new or failed files to update in DB for orchestration {OrchestrationId}. Skipped {SkippedCount} already processed files.",
                    orchestrationId, skippedAlreadyProcessed);
            }

            // Always recalculate and update state after discovery, based on the current runId
            var runDocsForState = await db.IngestedDocuments.Where(x => x.OrchestrationId == orchestrationId && x.RunId == runId).ToListAsync();
            _state.State.TotalFiles = runDocsForState.Count;
            _state.State.ProcessedFiles = runDocsForState.Count(x => x.IngestionState == IngestionState.Complete);
            _state.State.FailedFiles = runDocsForState.Count(x => x.IngestionState == IngestionState.Failed);
            await SafeWriteStateAsync();

            // If there's nothing to process (no docs or all are Complete/Failed), finish gracefully
            if (_state.State.TotalFiles == 0 || runDocsForState.All(x => x.IngestionState == IngestionState.Complete || x.IngestionState == IngestionState.Failed))
            {
                _state.State.Status = _state.State.FailedFiles > 0 ? IngestionOrchestrationState.Failed : IngestionOrchestrationState.Completed;
                await SafeWriteStateAsync();
                _isActive = false;
                _logger.LogInformation("No pending files for orchestration {OrchestrationId}. Marking as {Status}", orchestrationId, _state.State.Status);
                return;
            }
        }

        // Update state
        _state.State.Status = IngestionOrchestrationState.Running;
        await SafeWriteStateAsync();

        // Start or resume parallel file processing (global coordinator in child grains will enforce concurrency)
        await ResumeOrStartIngestionAsync(CancellationToken.None);
    }

    private async Task ResumeOrStartIngestionAsync(CancellationToken cancellationToken)
    {
        if (_currentRunId == null)
        {
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // The orchestration ID is calculated based on container path from outside the grain, making it
        // the same across all ingestion runs for a given container/folder.
        var orchestrationId = this.GetPrimaryKeyString();
        var files = await db.IngestedDocuments
            .Where(x => x.OrchestrationId == orchestrationId && x.RunId == _currentRunId &&
                        x.IngestionState != IngestionState.Complete
                        && x.IngestionState != IngestionState.Failed)
            .ToListAsync(cancellationToken);

        foreach (var file in files)
        {
            var fileGrain = GrainFactory.GetGrain<IFileIngestionGrain>(file.Id);
            if (await fileGrain.IsActiveAsync())
            {
                _logger.LogDebug("FileIngestionGrain for file {FileId} is already active, skipping start.", file.Id);
                continue;
            }

            // Stagger execution to resolve dependencies
            await Task.Delay(500, cancellationToken);

            // Increment active child count for each file started
            Interlocked.Increment(ref _activeChildCount);

            _ = fileGrain.StartIngestionAsync(file.Id)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogError(t.Exception, "Error starting ingestion for file {FileId}", file.Id);
                    }
                }, TaskScheduler.Default);
        }
    }

    public async Task OnIngestionCompletedAsync()
    {
        // Recalculate processed/failed/total from DB
        await using (var db = await _dbContextFactory.CreateDbContextAsync())
        {
            var orchestrationId = this.GetPrimaryKeyString();
            var allDocs = await db.IngestedDocuments.Where(x => x.OrchestrationId == orchestrationId && x.RunId == _currentRunId).ToListAsync();
            _state.State.TotalFiles = allDocs.Count;
            _state.State.ProcessedFiles = allDocs.Count(x => x.IngestionState == IngestionState.Complete);
            _state.State.FailedFiles = allDocs.Count(x => x.IngestionState == IngestionState.Failed);
            await SafeWriteStateAsync();

            // If all files are processed (successfully or with errors), mark as complete
            if (_state.State.ProcessedFiles + _state.State.FailedFiles >= _state.State.TotalFiles && _state.State.TotalFiles > 0)
            {
                _state.State.Status = _state.State.FailedFiles > 0 ? IngestionOrchestrationState.Failed : IngestionOrchestrationState.Completed;
                await SafeWriteStateAsync();

                _logger.LogInformation(
                    "Document ingestion for {DocumentLibraryName} completed. Processed: {ProcessedFiles}, Failed: {FailedFiles}, Total: {TotalFiles}",
                    _state.State.DocumentLibraryShortName, _state.State.ProcessedFiles, _state.State.FailedFiles, _state.State.TotalFiles);

                // Reset counters and errors for next run
                _state.State.TotalFiles = 0;
                _state.State.ProcessedFiles = 0;
                _state.State.FailedFiles = 0;
                _state.State.Errors = new List<string>();
                await SafeWriteStateAsync();

                // Clear in-memory activity when the orchestration finishes
                _isActive = false;
            }
        }
        // Decrement active child count
        Interlocked.Decrement(ref _activeChildCount);
    }

    public async Task OnIngestionFailedAsync(string reason, bool acquired)
    {
        // Recalculate processed/failed/total from DB
        await using (var db = await _dbContextFactory.CreateDbContextAsync())
        {
            var orchestrationId = this.GetPrimaryKeyString();
            var allDocs = await db.IngestedDocuments.Where(x => x.OrchestrationId == orchestrationId && x.RunId == _currentRunId).ToListAsync();
            _state.State.TotalFiles = allDocs.Count;
            _state.State.ProcessedFiles = allDocs.Count(x => x.IngestionState == IngestionState.Complete);
            _state.State.FailedFiles = allDocs.Count(x => x.IngestionState == IngestionState.Failed);
            _state.State.Errors.Add(reason);
            await SafeWriteStateAsync();

            // If all files are processed (successfully or with errors), mark as complete
            if (_state.State.ProcessedFiles + _state.State.FailedFiles >= _state.State.TotalFiles && _state.State.TotalFiles > 0)
            {
                _state.State.Status = IngestionOrchestrationState.Failed;
                await SafeWriteStateAsync();

                _logger.LogError(
                    "Document ingestion for {DocumentLibraryName} completed with errors. Processed: {ProcessedFiles}, Failed: {FailedFiles}, Total: {TotalFiles}",
                    _state.State.DocumentLibraryShortName, _state.State.ProcessedFiles, _state.State.FailedFiles, _state.State.TotalFiles);

                // Reset counters and errors for next run
                _state.State.TotalFiles = 0;
                _state.State.ProcessedFiles = 0;
                _state.State.FailedFiles = 0;
                _state.State.Errors = new List<string>();
                await SafeWriteStateAsync();

                // Clear in-memory activity when the orchestration finishes with failure
                _isActive = false;
            }
        }
        // Decrement active child count
        Interlocked.Decrement(ref _activeChildCount);
    }

    private async Task SafeWriteStateAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            _state.State.LastUpdatedUtc = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    // Basic filename sanitization consistent with vector store usage
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return $"unknown_file_{Guid.NewGuid():N}";
        }

        return fileName
            .Replace(" ", "_")
            .Replace("+", "_")
            .Replace("~", "_")
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace(":", "_")
            .Replace("*", "_")
            .Replace("?", "_")
            .Replace("\"", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace("|", "_");
    }

    // URL-safe Base64 encode without padding to match repository document id scheme
    private static string Base64UrlEncode(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Resolves the FileStorageSourceId for the given document library/process and container.
    /// This method handles both legacy blob container names and new FileStorageSource ID patterns.
    /// </summary>
    private static async Task<Guid?> GetFileStorageSourceIdAsync(
        DocGenerationDbContext db,
        DocumentLibraryType documentLibraryType,
        string documentLibraryShortName,
        string containerName)
    {
        try
        {
            // Check if container name is in the new FileStorageSource ID format
            if (containerName.StartsWith("FileStorageSource:", StringComparison.Ordinal))
            {
                var sourceIdString = containerName.Substring("FileStorageSource:".Length);
                if (Guid.TryParse(sourceIdString, out var sourceId))
                {
                    return sourceId;
                }
            }

            // Fall back to legacy resolution by container name for backward compatibility
            if (documentLibraryType == DocumentLibraryType.AdditionalDocumentLibrary)
            {
                // Find the document library
                var documentLibrary = await db.DocumentLibraries
                    .FirstOrDefaultAsync(dl => dl.ShortName == documentLibraryShortName);

                if (documentLibrary == null)
                {
                    return null;
                }

                // Find a FileStorageSource associated with this document library that matches the container
                var fileStorageSource = await db.DocumentLibraryFileStorageSources
                    .Include(dlfs => dlfs.FileStorageSource)
                    .Where(dlfs => dlfs.DocumentLibraryId == documentLibrary.Id)
                    .Select(dlfs => dlfs.FileStorageSource)
                    .FirstOrDefaultAsync(fss => fss.ContainerOrPath == containerName || 
                                               (fss.ContainerOrPath == null && containerName == "default-container"));

                return fileStorageSource?.Id;
            }
            else // PrimaryDocumentProcessLibrary
            {
                // Find the document process
                var documentProcess = await db.DynamicDocumentProcessDefinitions
                    .FirstOrDefaultAsync(dp => dp.ShortName == documentLibraryShortName);

                if (documentProcess == null)
                {
                    return null;
                }

                // Find a FileStorageSource associated with this document process that matches the container
                var fileStorageSource = await db.DocumentProcessFileStorageSources
                    .Include(dpfs => dpfs.FileStorageSource)
                    .Where(dpfs => dpfs.DocumentProcessId == documentProcess.Id)
                    .Select(dpfs => dpfs.FileStorageSource)
                    .FirstOrDefaultAsync(fss => fss.ContainerOrPath == containerName || 
                                               (fss.ContainerOrPath == null && containerName == "default-container"));

                return fileStorageSource?.Id;
            }
        }
        catch (Exception)
        {
            // If we can't determine the FileStorageSourceId, return null to skip acknowledgment record creation
            return null;
        }
    }

    /// <summary>
    /// Cleans up orphaned IngestedDocument records that are causing unique constraint violations and retries the operation.
    /// </summary>
    /// <param name="db">The database context</param>
    /// <param name="fileStorageSourceId">The file storage source ID</param>
    /// <param name="dlDpList">The list of document libraries/processes</param>
    /// <param name="folderPath">The folder path being processed</param>
    /// <param name="orchestrationId">The current orchestration ID</param>
    /// <param name="runId">The current run ID</param>
    /// <returns>The number of orphaned records cleaned up</returns>
    private async Task<int> CleanupOrphanedIngestedDocumentsAndRetryAsync(
        DocGenerationDbContext db,
        Guid fileStorageSourceId,
        List<(string shortName, Guid id, DocumentLibraryType libraryType, bool isDocumentLibrary)> dlDpList,
        string folderPath,
        string orchestrationId,
        string runId)
    {
        int cleanedUpCount = 0;

        try
        {
            // Discard all pending changes first since they caused the constraint violation
            foreach (var entry in db.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Detached;
                }
            }

            var containerName = $"FileStorageSource:{fileStorageSourceId}";

            // Find orphaned IngestedDocument records for each DL/DP in this container + folder combination
            foreach (var (shortName, id, libraryType, isDocumentLibrary) in dlDpList)
            {
                var orphanedDocuments = await db.IngestedDocuments
                    .Where(doc => doc.DocumentLibraryType == libraryType &&
                                 doc.DocumentLibraryOrProcessName == shortName &&
                                 doc.Container == containerName &&
                                 doc.FolderPath == folderPath &&
                                 // Exclude current orchestration/run to avoid deleting valid in-progress records
                                 (doc.OrchestrationId != orchestrationId || doc.RunId.ToString() != runId))
                    .ToListAsync();

                if (orphanedDocuments.Any())
                {
                    _logger.LogInformation("Found {Count} orphaned IngestedDocument records for {LibraryOrProcessType} '{ShortName}' in container '{Container}' and folder '{FolderPath}'",
                        orphanedDocuments.Count, 
                        isDocumentLibrary ? "DocumentLibrary" : "DocumentProcess", 
                        shortName, 
                        containerName, 
                        folderPath);

                    // Remove their FileAcknowledgment relationships first
                    var documentIds = orphanedDocuments.Select(d => d.Id).ToList();
                    var acknowledgmentRelationships = await db.IngestedDocumentFileAcknowledgments
                        .Where(idfa => documentIds.Contains(idfa.IngestedDocumentId))
                        .ToListAsync();

                    if (acknowledgmentRelationships.Any())
                    {
                        db.IngestedDocumentFileAcknowledgments.RemoveRange(acknowledgmentRelationships);
                        _logger.LogInformation("Removing {Count} orphaned IngestedDocumentFileAcknowledgment relationships", 
                            acknowledgmentRelationships.Count);
                    }

                    // Remove the orphaned IngestedDocuments
                    db.IngestedDocuments.RemoveRange(orphanedDocuments);
                    cleanedUpCount += orphanedDocuments.Count;

                    _logger.LogInformation("Removing {Count} orphaned IngestedDocument records", orphanedDocuments.Count);
                }
            }

            if (cleanedUpCount > 0)
            {
                // Save the cleanup changes
                await db.SaveChangesAsync();

                // Now retry creating the new IngestedDocuments and relationships
                // We need to recreate the changes that were discarded earlier

                // Get fresh FileAcknowledgmentRecords for the current folder
                var acknowledgmentRecords = await db.FileAcknowledgmentRecords
                    .Where(far => far.FileStorageSourceId == fileStorageSourceId &&
                                 far.RelativeFilePath.StartsWith(folderPath))
                    .ToListAsync();

                int newFilesRetry = 0;

                foreach (var ackRecord in acknowledgmentRecords)
                {
                    // Check existing IngestedDocuments for this acknowledged file
                    var existingIngestedDocs = await db.IngestedDocuments
                        .Where(doc => doc.IngestedDocumentFileAcknowledgments
                            .Any(idfa => idfa.FileAcknowledgmentRecordId == ackRecord.Id))
                        .ToListAsync();

                    foreach (var (shortName, id, libraryType, isDocumentLibrary) in dlDpList)
                    {
                        if (!existingIngestedDocs.Any(doc => doc.DocumentLibraryOrProcessName == shortName &&
                                                             doc.DocumentLibraryType == libraryType))
                        {
                            // Recreate missing IngestedDocument for this DL/DP
                            var fileName = System.IO.Path.GetFileName(ackRecord.RelativeFilePath);
                            var ingestedDocument = new IngestedDocument
                            {
                                Id = Guid.NewGuid(),
                                FileName = fileName,
                                Container = containerName,
                                FolderPath = folderPath,
                                OrchestrationId = orchestrationId,
                                RunId = Guid.Parse(runId),
                                IngestionState = IngestionState.DiscoveredForConsumer,
                                IngestedDate = DateTime.UtcNow,
                                OriginalDocumentUrl = ackRecord.FileStorageSourceInternalUrl,
                                FinalBlobUrl = ackRecord.FileStorageSourceInternalUrl,
                                DocumentLibraryType = libraryType,
                                DocumentLibraryOrProcessName = shortName,
                                FileHash = ackRecord.FileHash
                            };

                            db.IngestedDocuments.Add(ingestedDocument);

                            // Link to the existing FileAcknowledgmentRecord
                            var acknowledgmentLink = new IngestedDocumentFileAcknowledgment
                            {
                                Id = Guid.NewGuid(),
                                IngestedDocumentId = ingestedDocument.Id,
                                FileAcknowledgmentRecordId = ackRecord.Id
                            };
                            db.IngestedDocumentFileAcknowledgments.Add(acknowledgmentLink);

                            newFilesRetry++;

                            _logger.LogInformation("Recreated IngestedDocument for acknowledged file {FileName} for DL/DP {ShortName} after cleanup",
                                fileName, shortName);
                        }
                    }
                }

                if (newFilesRetry > 0)
                {
                    // Save the recreated records
                    await db.SaveChangesAsync();
                    _logger.LogInformation("Successfully recreated {NewFilesRetry} IngestedDocument records after cleanup", newFilesRetry);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during orphaned IngestedDocument cleanup and recovery. Ingestion will continue without these files.");
            
            // If recovery fails, discard all changes to avoid leaving the context in a bad state
            foreach (var entry in db.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        return cleanedUpCount;
    }
}
