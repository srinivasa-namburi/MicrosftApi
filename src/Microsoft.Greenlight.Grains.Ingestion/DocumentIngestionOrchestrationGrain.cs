// Copyright (c) Microsoft Corporation. All rights reserved.

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
        ISemanticKernelVectorStoreProvider vectorStoreProvider)
    {
        _state = state;
        _logger = logger;
        _optionsSnapshot = optionsSnapshot;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
        _dbContextFactory = dbContextFactory;
        _azureFileHelper = azureFileHelper;
        _vectorStoreProvider = vectorStoreProvider;
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

        // Validate document library/process exists and get target container
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
            _state.State.TargetContainerName = documentLibrary.BlobStorageContainerName;
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
            _state.State.TargetContainerName = documentProcess.BlobStorageContainerName;
        }

        // Use hashing grain to enumerate and hash blobs in parallel.
        var hashingGrainKey = orchestrationId; // tie hashing to orchestration
        var hashingGrain = GrainFactory.GetGrain<IBlobHashingGrain>(hashingGrainKey);
        if (await hashingGrain.IsActiveAsync())
        {
            _logger.LogInformation("Hashing already active for orchestration {OrchestrationId}. Backing off this cycle.", orchestrationId);
            _isActive = false; // let scheduler retry without blocking
            return; // let the scheduler retry on its next cycle
        }

        List<BlobHashInfo> blobHashes = await hashingGrain.StartHashingAsync(_state.State.TargetContainerName, folderPath, runId);

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
                        d.FileHash == currentHash);

                    // Backfill hash if we only have a name match without hash stored
                    if (existingDoc == null)
                    {
                        var existingByName = allExistingDocsForOrchestration.FirstOrDefault(d =>
                            d.FolderPath == folderPath &&
                            d.FileName == relativeFileName &&
                            d.Container == _state.State.TargetContainerName);
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
                        d.Container == _state.State.TargetContainerName);
                }

                if (existingDoc == null)
                {
                    _logger.LogInformation("New file discovered: Container={Container}, FolderPath={FolderPath}, FileName={RelativeFileName}",
                        _state.State.TargetContainerName, folderPath, relativeFileName);
                    db.IngestedDocuments.Add(new IngestedDocument
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
                    });
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
                                    // Already processed and present, delete from source to prevent reprocessing
                                    _logger.LogInformation("Document {ExistingDocFileName} in folder {ExistingDocFolderPath} already processed and complete. Deleting from source location.",
                                        existingDoc.FileName, existingDoc.FolderPath);
                                    var containerClient = _azureFileHelper.GetBlobServiceClient().GetBlobContainerClient(_state.State.TargetContainerName);
                                    await containerClient.GetBlobClient(entry.BlobName).DeleteIfExistsAsync();
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
}