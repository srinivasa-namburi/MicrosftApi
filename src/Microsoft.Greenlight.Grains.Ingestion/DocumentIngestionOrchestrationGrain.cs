using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.State;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Orleans.Concurrency;

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
    private SemaphoreSlim _processingThrottleSemaphore;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly AzureFileHelper _azureFileHelper;

    // In-memory counter for active child processes
    private int _activeChildCount = 0;
    // In-memory RunId for the current ingestion run
    private Guid? _currentRunId = null;

    public DocumentIngestionOrchestrationGrain(
        [PersistentState("docIngestOrchestration")]
        IPersistentState<DocumentIngestionState> state,
        ILogger<DocumentIngestionOrchestrationGrain> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        AzureFileHelper azureFileHelper)
    {
        _state = state;
        _logger = logger;
        _optionsSnapshot = optionsSnapshot;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
        _dbContextFactory = dbContextFactory;
        _azureFileHelper = azureFileHelper;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Initialize the grain state if it's new
        if (string.IsNullOrWhiteSpace(_state.State.Id))
        {
            _state.State.Id = this.GetPrimaryKeyString();
            _state.State.Status = IngestionOrchestrationState.NotStarted;
            _state.State.Errors = new List<string>();
            await SafeWriteStateAsync();
        }

        // Initialize the semaphore when the grain activates
        var maxWorkers = _optionsSnapshot.Value.GreenlightServices.Scalability.NumberOfIngestionWorkers;
        if (maxWorkers <= 0)
            maxWorkers = 4; // Default to 4 workers if not configured

        _processingThrottleSemaphore = new SemaphoreSlim(maxWorkers, maxWorkers);

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task ResumeOrStartIngestionAsync(CancellationToken cancellationToken)
    {
        if (_currentRunId == null)
            return;
        await using var db = _dbContextFactory.CreateDbContext();
        var orchestrationId = this.GetPrimaryKeyString();
        var files = await db.IngestedDocuments
            .Where(x => x.OrchestrationId == orchestrationId && x.RunId == _currentRunId && x.IngestionState != IngestionState.Complete && x.IngestionState != IngestionState.Failed)
            .ToListAsync(cancellationToken);

        foreach (var file in files)
        {
            var fileGrain = GrainFactory.GetGrain<IFileIngestionGrain>(file.Id);
            if (await fileGrain.IsActiveAsync())
            {
                _logger.LogDebug("FileIngestionGrain for file {FileId} is already active, skipping start.", file.Id);
                continue;
            }

            await _processingThrottleSemaphore.WaitAsync(TimeSpan.FromMinutes(15));
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

                    try
                    {
                        _processingThrottleSemaphore.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                        // There was no semaphore to release
                    }
                }, TaskScheduler.Current);
        }
    }

    public async Task<DocumentIngestionState> GetStateAsync()
    {
        return _state.State;
    }

    public async Task StartIngestionAsync(
        string documentLibraryShortName,
        DocumentLibraryType documentLibraryType,
        string blobContainerName,
        string folderPath)
    {
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
                    return;
                }
            }
            // If no files left and state is Running, mark as complete/failed
            if (_state.State.Status == IngestionOrchestrationState.Running && !runDocs.Any() && _state.State.TotalFiles > 0)
            {
                _state.State.Status = _state.State.FailedFiles > 0 ? IngestionOrchestrationState.Failed : IngestionOrchestrationState.Completed;
                await SafeWriteStateAsync();
                _logger.LogInformation("[StartIngestionAsync] Orchestration {OrchestrationId} marked as {Status} (no files left)", orchestrationId, _state.State.Status);
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
                return;
            }
            _state.State.TargetContainerName = documentProcess.BlobStorageContainerName;
        }

        // Enumerate blobs in the container/folder and add new IngestedDocument entries if needed
        await using (var db = await _dbContextFactory.CreateDbContextAsync())
        {
            // Fetch all existing documents for this orchestration to perform in-memory lookups or updates.
            var allExistingDocsForOrchestration = db.IngestedDocuments
                .Where(x => x.OrchestrationId == orchestrationId);
              

            var containerClient = _azureFileHelper.GetBlobServiceClient().GetBlobContainerClient(_state.State.TargetContainerName);
            await containerClient.CreateIfNotExistsAsync();

            _logger.LogDebug("Enumerating blobs in container {ContainerName} with prefix {FolderPath}", _state.State.TargetContainerName, folderPath);
            var blobs = containerClient.GetBlobs(prefix: folderPath);
            int newFiles = 0;
            int resetFiles = 0;
            int skippedAlreadyProcessed = 0;

            foreach (var blob in blobs)
            {
                string relativeFileName = System.IO.Path.GetFileName(blob.Name);
                if (string.IsNullOrEmpty(relativeFileName) || blob.Name.TrimEnd('/').Equals(folderPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                {
                    // This can happen if the prefix itself is listed as a blob, or if Path.GetFileName returns empty for a directory-like structure.
                    // We are interested in files, so skip such entries.
                    _logger.LogDebug("Skipping blob entry {BlobName} as it appears to be a folder or has no filename component relative to folderPath {FolderPath}.", blob.Name, folderPath);
                    continue;
                }

                var existingDoc = allExistingDocsForOrchestration.FirstOrDefault(d =>
                                    d.FolderPath == folderPath &&
                                    d.FileName == relativeFileName &&
                                    d.Container == _state.State.TargetContainerName);

                if (existingDoc == null)
                {
                    _logger.LogInformation("New file discovered: Container={Container}, FolderPath={FolderPath}, FileName={RelativeFileName} (Original Blob Path: {BlobName})",
                        _state.State.TargetContainerName, folderPath, relativeFileName, blob.Name);
                    db.IngestedDocuments.Add(new Microsoft.Greenlight.Shared.Models.IngestedDocument
                    {
                        Id = Guid.NewGuid(),
                        FileName = relativeFileName, // Store relative file name
                        Container = _state.State.TargetContainerName,
                        FolderPath = folderPath, // Store the scanned folder path
                        OrchestrationId = orchestrationId,
                        RunId = runId,
                        IngestionState = IngestionState.Discovered,
                        IngestedDate = DateTime.UtcNow,
                        OriginalDocumentUrl = containerClient.GetBlobClient(blob.Name).Uri.ToString(), // Full path for URL
                        DocumentLibraryType = documentLibraryType,
                        DocumentLibraryOrProcessName = documentLibraryShortName
                    });
                    newFiles++;
                }
                else // Document already exists in DB for this OrchestrationId, FolderPath, and FileName
                {
                    if (existingDoc.IngestionState == IngestionState.Failed)
                    {
                        _logger.LogInformation("Resetting failed document for re-ingestion: {ExistingDocFileName} in folder {ExistingDocFolderPath}", existingDoc.FileName, existingDoc.FolderPath);
                        existingDoc.IngestionState = IngestionState.Discovered;
                        existingDoc.IngestedDate = DateTime.UtcNow;
                        existingDoc.OriginalDocumentUrl = containerClient.GetBlobClient(blob.Name).Uri.ToString();
                        existingDoc.RunId = runId;
                        existingDoc.Error = null;
                        resetFiles++;
                    }
                    else if (existingDoc.IngestionState == IngestionState.Complete)
                    {
                        // If the document is already marked as complete in the DB for this orchestration, folder, and file name,
                        // we can assume it was processed successfully in a previous run or by another mechanism.
                        // Delete the blob from the source/auto-import folder to prevent reprocessing.
                        _logger.LogInformation("Document {ExistingDocFileName} in folder {ExistingDocFolderPath} already processed and complete. Deleting from source location {BlobName}.",
                            existingDoc.FileName, existingDoc.FolderPath, blob.Name);
                        await containerClient.GetBlobClient(blob.Name).DeleteIfExistsAsync();
                        skippedAlreadyProcessed++;
                    }
                    else
                    {
                        // Document exists but is in a state like Discovered, FileCopying, Processing from a previous/concurrent run.
                        // If it's not associated with the current runId yet, and not actively being processed by a file grain,
                        // it might be an abandoned file from a previous run. The logic at the start of StartIngestionAsync
                        // should have reassigned its RunId if it was eligible.
                        // If it was already assigned to this runId by that logic, it will be picked up by ResumeOrStartIngestionAsync.
                        _logger.LogDebug("Document {ExistingDocFileName} in folder {ExistingDocFolderPath} already exists with state {IngestionState}. It will be handled by the current run if eligible.",
                            existingDoc.FileName, existingDoc.FolderPath, existingDoc.IngestionState);
                    }
                }
            }

            if (newFiles > 0 || resetFiles > 0)
            {
                await db.SaveChangesAsync();
                _logger.LogInformation("Committed DB changes: Added {NewCount} new files and reset {ResetCount} failed files for orchestration {OrchestrationId}. Skipped {SkippedCount} already processed files.",
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
        }

        // Update state
        _state.State.Status = IngestionOrchestrationState.Running;
        await SafeWriteStateAsync();

        // Start or resume parallel file processing
        await ResumeOrStartIngestionAsync(CancellationToken.None);
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
            }
        }
        // Release a slot in the semaphore for the next document
        ReleaseSemaphore();
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
            }
        }
        // Release a slot in the semaphore for the next document
        if (acquired)
        {
            ReleaseSemaphore();
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

    private void ReleaseSemaphore()
    {
        try
        {
            _processingThrottleSemaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // There was no semaphore to release, this can happen if the semaphore was already released

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing semaphore");
        }
    }
}