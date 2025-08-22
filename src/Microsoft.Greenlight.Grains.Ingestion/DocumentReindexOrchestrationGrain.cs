// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.State;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages.Reindexing.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Ingestion;

/// <summary>
/// Orchestrates document reindexing operations for document libraries and processes.
/// </summary>
[Reentrant]
public class DocumentReindexOrchestrationGrain : Grain, IDocumentReindexOrchestrationGrain
{
    private readonly ILogger<DocumentReindexOrchestrationGrain> _logger;
    private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly IPersistentState<DocumentReindexState> _state;

    // Separate locks for different concerns to reduce contention
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _progressLock = new(1, 1);
    private SemaphoreSlim _processingThrottleSemaphore;

    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IDocumentIngestionService _documentIngestionService;

    // Thread-safe counters for progress tracking
    private volatile int _processedCount = 0;
    private volatile int _failedCount = 0;

    // In-memory activity guard to prevent overlapping runs per grain key
    private volatile bool _isActive = false;

    public DocumentReindexOrchestrationGrain(
        [PersistentState("docReindexOrchestration")]
        IPersistentState<DocumentReindexState> state,
        ILogger<DocumentReindexOrchestrationGrain> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IDocumentIngestionService documentIngestionService)
    {
        _state = state;
        _logger = logger;
        _optionsSnapshot = optionsSnapshot;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
        _dbContextFactory = dbContextFactory;
        _documentIngestionService = documentIngestionService;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Initialize the grain state if it's new
        if (string.IsNullOrWhiteSpace(_state.State.Id))
        {
            _state.State.Id = this.GetPrimaryKeyString();
            _state.State.Status = ReindexOrchestrationState.NotStarted;
            _state.State.Errors = new List<string>();
            await _state.WriteStateAsync(); // Direct write without nested locking
        }

        // Initialize the semaphore when the grain activates
        var maxWorkers = _optionsSnapshot.Value.GreenlightServices.Scalability.NumberOfIngestionWorkers;
        if (maxWorkers <= 0)
        {
            maxWorkers = 4; // Default to 4 workers if not configured
        }

        _processingThrottleSemaphore = new SemaphoreSlim(maxWorkers, maxWorkers);

        // Sync in-memory counters with persisted state
        _processedCount = _state.State.ProcessedDocuments;
        _failedCount = _state.State.FailedDocuments;

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<DocumentReindexState> GetStateAsync()
    {
        // Return a snapshot to avoid external mutation
        return new DocumentReindexState
        {
            Id = _state.State.Id,
            DocumentLibraryShortName = _state.State.DocumentLibraryShortName,
            DocumentLibraryType = _state.State.DocumentLibraryType,
            Status = _state.State.Status,
            Reason = _state.State.Reason,
            StartedUtc = _state.State.StartedUtc,
            CompletedUtc = _state.State.CompletedUtc,
            LastUpdatedUtc = _state.State.LastUpdatedUtc,
            TotalDocuments = _state.State.TotalDocuments,
            ProcessedDocuments = _processedCount, // Use in-memory counter for latest value
            FailedDocuments = _failedCount, // Use in-memory counter for latest value
            TargetContainerName = _state.State.TargetContainerName,
            Errors = new List<string>(_state.State.Errors)
        };
    }

    public async Task StartReindexingAsync(
        string documentLibraryShortName,
        DocumentLibraryType documentLibraryType,
        string reason)
    {
        // Prevent overlapping runs for the same grain activation/key
        if (_isActive)
        {
            _logger.LogInformation("Reindex orchestration {OrchestrationId} already active. Skipping duplicate start.", this.GetPrimaryKeyString());
            return;
        }

        await _stateLock.WaitAsync();
        try
        {
            if (_state.State.Status == ReindexOrchestrationState.Running)
            {
                _logger.LogWarning("Reindexing already running for {DocumentLibraryName}", documentLibraryShortName);
                return;
            }

            // Initialize state
            _state.State.Id = this.GetPrimaryKeyString();
            _state.State.DocumentLibraryShortName = documentLibraryShortName;
            _state.State.DocumentLibraryType = documentLibraryType;
            _state.State.Status = ReindexOrchestrationState.Running;
            _state.State.Reason = reason;
            _state.State.StartedUtc = DateTime.UtcNow;
            _state.State.TotalDocuments = 0;
            _state.State.ProcessedDocuments = 0;
            _state.State.FailedDocuments = 0;
            _state.State.LastUpdatedUtc = DateTime.UtcNow;
            _state.State.Errors.Clear();

            // Reset in-memory counters
            _processedCount = 0;
            _failedCount = 0;

            await _state.WriteStateAsync(); // Direct write without nested locking

            // Send started notification
            var signalRGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            await signalRGrain.NotifyDocumentReindexStartedAsync(new DocumentReindexStartedNotification(
                _state.State.Id,
                documentLibraryShortName,
                reason));

            _logger.LogInformation("Starting reindexing for {DocumentLibraryName} with reason: {Reason}",
                documentLibraryShortName, reason);

            // Mark this activation as active only after state init
            _isActive = true;
        }
        finally
        {
            _stateLock.Release();
        }

        // Continue processing outside of the state lock
        await ContinueReindexingAsync(documentLibraryShortName, documentLibraryType);
    }

    private async Task ContinueReindexingAsync(string documentLibraryShortName, DocumentLibraryType documentLibraryType)
    {
        var signalRGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);

        try
        {
            // Validate document library exists and get target container and index name
            string targetContainer = null;
            string indexName = null;

            if (documentLibraryType == DocumentLibraryType.AdditionalDocumentLibrary)
            {
                var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);
                if (documentLibrary == null)
                {
                    await HandleFailureAsync($"Document library {documentLibraryShortName} not found", signalRGrain);
                    return;
                }

                if (documentLibrary.LogicType != DocumentProcessLogicType.SemanticKernelVectorStore)
                {
                    await HandleFailureAsync($"Reindexing is only supported for document libraries using SemanticKernelVectorStore logic type. Library {documentLibraryShortName} uses {documentLibrary.LogicType}", signalRGrain);
                    return;
                }

                targetContainer = documentLibrary.BlobStorageContainerName;
                indexName = documentLibrary.IndexName;
            }
            else // PrimaryDocumentProcessLibrary
            {
                var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryShortName);
                if (documentProcess == null)
                {
                    await HandleFailureAsync($"Document process {documentLibraryShortName} not found", signalRGrain);
                    return;
                }

                if (documentProcess.LogicType != DocumentProcessLogicType.SemanticKernelVectorStore)
                {
                    await HandleFailureAsync($"Reindexing is only supported for document processes using SemanticKernelVectorStore logic type. Process {documentLibraryShortName} uses {documentProcess.LogicType}", signalRGrain);
                    return;
                }

                targetContainer = documentProcess.BlobStorageContainerName;
                indexName = documentProcess.Repositories?.FirstOrDefault() ?? documentLibraryShortName;
            }

            // Update target container
            await _stateLock.WaitAsync();
            try
            {
                _state.State.TargetContainerName = targetContainer;
                _state.State.LastUpdatedUtc = DateTime.UtcNow;
                await _state.WriteStateAsync();
            }
            finally
            {
                _stateLock.Release();
            }

            // Step 1: Clear the vector store index (use the actual index name)
            await ClearVectorStoreAsync(documentLibraryShortName, indexName);

            // Step 2: Find all ingested documents for this library/process that were previously indexed
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var documentsToReindex = await db.IngestedDocuments
                .Where(d => d.DocumentLibraryOrProcessName == documentLibraryShortName &&
                           d.DocumentLibraryType == documentLibraryType &&
                           d.IngestionState == IngestionState.Complete &&
                           (d.IsVectorStoreIndexed == true || d.VectorStoreIndexedDate != null))
                .ToListAsync();

            // Update total documents count
            await _stateLock.WaitAsync();
            try
            {
                _state.State.TotalDocuments = documentsToReindex.Count;
                _state.State.LastUpdatedUtc = DateTime.UtcNow;
                await _state.WriteStateAsync();
            }
            finally
            {
                _stateLock.Release();
            }

            _logger.LogInformation("Found {DocumentCount} documents to reindex for {LibraryName}",
                documentsToReindex.Count, documentLibraryShortName);

            if (documentsToReindex.Count == 0)
            {
                await CompleteReindexingAsync(true, signalRGrain);
                return;
            }

            // Step 3: Start parallel reindexing (do not mass-reset flags here)
            await StartParallelReindexingAsync(documentsToReindex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reindexing initialization for {LibraryName}", documentLibraryShortName);
            await HandleFailureAsync($"Initialization error: {ex.Message}", signalRGrain);
        }
    }

    private async Task HandleFailureAsync(string errorMessage, ISignalRNotifierGrain signalRGrain)
    {
        await _stateLock.WaitAsync();
        try
        {
            _state.State.Status = ReindexOrchestrationState.Failed;
            _state.State.CompletedUtc = DateTime.UtcNow;
            _state.State.LastUpdatedUtc = DateTime.UtcNow;
            _state.State.Errors.Add(errorMessage);
            await _state.WriteStateAsync();
        }
        finally
        {
            _stateLock.Release();
        }

        _logger.LogError("Reindexing failed: {Error}", errorMessage);

        await signalRGrain.NotifyDocumentReindexFailedAsync(new DocumentReindexFailedNotification(
            _state.State.Id,
            _state.State.DocumentLibraryShortName,
            errorMessage));

        // Clear active flag on failure
        _isActive = false;
    }

    private async Task ClearVectorStoreAsync(string contextName, string indexName)
    {
        try
        {
            _logger.LogInformation("Clearing vector store index for {Context} (index={IndexName})", contextName, indexName);
            await _documentIngestionService.ClearIndexAsync(contextName, indexName);
            _logger.LogInformation("Successfully cleared vector store index for {Context} (index={IndexName})", contextName, indexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing vector store index for {Context} (index={IndexName})", contextName, indexName);
            throw;
        }
    }

    private async Task StartParallelReindexingAsync(List<Microsoft.Greenlight.Shared.Models.IngestedDocument> documents)
    {
        var tasks = new List<Task>();

        foreach (var document in documents)
        {
            // Create a task that handles semaphore acquisition and processing
            var task = ProcessDocumentWithThrottlingAsync(document);
            tasks.Add(task);
        }

        // Don't await all tasks here - let them run in background
        // The completion will be handled by the callback methods
        _ = Task.WhenAll(tasks).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "Error in parallel reindexing tasks");
            }
        }, TaskScheduler.Default);
    }

    private async Task ProcessDocumentWithThrottlingAsync(Microsoft.Greenlight.Shared.Models.IngestedDocument document)
    {
        // Avoid indefinite waits that could stall the whole run; retry on timeout.
        var acquired = await _processingThrottleSemaphore.WaitAsync(TimeSpan.FromMinutes(15));
        if (!acquired)
        {
            _logger.LogWarning("Timeout acquiring reindex throttle for document {DocumentId}; will retry shortly.", document.Id);
            _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => ProcessDocumentWithThrottlingAsync(document), TaskScheduler.Default);
            return;
        }

        try
        {
            var reindexGrain = GrainFactory.GetGrain<IDocumentReindexProcessorGrain>(document.Id);
            // Pass the orchestration grain ID so the processor can reference back to this grain
            await reindexGrain.StartReindexingAsync(document.Id, _state.State.Reason, _state.State.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting reindexing for document {DocumentId}", document.Id);
            // Handle as a failed document
            await OnReindexFailedAsync($"Error starting reindexing: {ex.Message}", true);
        }
        finally
        {
            _processingThrottleSemaphore.Release();
        }
    }

    public async Task OnReindexCompletedAsync()
    {
        var newProcessedCount = Interlocked.Increment(ref _processedCount);

        await UpdateProgressAndCheckCompletionAsync(newProcessedCount, _failedCount, null);
    }

    public async Task OnReindexFailedAsync(string errorMessage, bool acquired)
    {
        var newFailedCount = Interlocked.Increment(ref _failedCount);

        await UpdateProgressAndCheckCompletionAsync(_processedCount, newFailedCount, errorMessage);
    }

    private async Task UpdateProgressAndCheckCompletionAsync(int processedCount, int failedCount, string errorMessage)
    {
        bool shouldComplete = false;
        bool isSuccess = false;

        // Use a separate lock for progress updates to avoid blocking state operations
        await _progressLock.WaitAsync();
        try
        {
            // Add error if provided
            if (!string.IsNullOrEmpty(errorMessage))
            {
                await _stateLock.WaitAsync();
                try
                {
                    _state.State.Errors.Add(errorMessage);
                    _state.State.LastUpdatedUtc = DateTime.UtcNow;
                    await _state.WriteStateAsync();
                }
                finally
                {
                    _stateLock.Release();
                }
            }

            // Send progress notification
            await SendProgressNotificationAsync(processedCount, failedCount);

            // Check if all documents are processed
            if (processedCount + failedCount >= _state.State.TotalDocuments)
            {
                shouldComplete = true;
                isSuccess = failedCount == 0;
            }
        }
        finally
        {
            _progressLock.Release();
        }

        if (shouldComplete)
        {
            var signalRGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            await CompleteReindexingAsync(isSuccess, signalRGrain);
        }
    }

    private async Task CompleteReindexingAsync(bool isSuccess, ISignalRNotifierGrain signalRGrain)
    {
        await _stateLock.WaitAsync();
        try
        {
            _state.State.Status = isSuccess ? ReindexOrchestrationState.Completed : ReindexOrchestrationState.Failed;
            _state.State.CompletedUtc = DateTime.UtcNow;
            _state.State.LastUpdatedUtc = DateTime.UtcNow;
            _state.State.ProcessedDocuments = _processedCount;
            _state.State.FailedDocuments = _failedCount;
            await _state.WriteStateAsync();
        }
        finally
        {
            _stateLock.Release();
        }

        if (isSuccess)
        {
            await signalRGrain.NotifyDocumentReindexCompletedAsync(new DocumentReindexCompletedNotification(
                _state.State.Id,
                _state.State.DocumentLibraryShortName,
                _state.State.TotalDocuments,
                _processedCount,
                _failedCount,
                true));

            _logger.LogInformation("Document reindexing completed successfully for {DocumentLibraryName}. Processed: {Processed}, Failed: {Failed}",
                _state.State.DocumentLibraryShortName, _processedCount, _failedCount);
        }
        else
        {
            await signalRGrain.NotifyDocumentReindexFailedAsync(new DocumentReindexFailedNotification(
                _state.State.Id,
                _state.State.DocumentLibraryShortName,
                string.Join("; ", _state.State.Errors.TakeLast(3))));

            _logger.LogError("Document reindexing failed for {DocumentLibraryName}. Processed: {Processed}, Failed: {Failed}",
                _state.State.DocumentLibraryShortName, _processedCount, _failedCount);
        }

        // Clear active flag once orchestration run completes
        _isActive = false;
    }

    private async Task SendProgressNotificationAsync(int processedCount, int failedCount)
    {
        var signalRGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        await signalRGrain.NotifyDocumentReindexProgressAsync(new DocumentReindexProgressNotification(
            _state.State.Id,
            _state.State.DocumentLibraryShortName,
            _state.State.TotalDocuments,
            processedCount,
            failedCount));
    }

    public async Task StartDocumentProcessReindexingAsync(string documentProcessShortName, string reason)
    {
        await StartReindexingAsync(documentProcessShortName, DocumentLibraryType.PrimaryDocumentProcessLibrary, reason);
    }

    public async Task StartDocumentLibraryReindexingAsync(string documentLibraryShortName, string reason)
    {
        await StartReindexingAsync(documentLibraryShortName, DocumentLibraryType.AdditionalDocumentLibrary, reason);
    }

}