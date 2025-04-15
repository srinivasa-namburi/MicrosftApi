using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.State;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
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

    public DocumentIngestionOrchestrationGrain(
        [PersistentState("documentIngestion")]
        IPersistentState<DocumentIngestionState> state,
        ILogger<DocumentIngestionOrchestrationGrain> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService)
    {
        _state = state;
        _logger = logger;
        _optionsSnapshot = optionsSnapshot;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Initialize the grain state if it's new
        if (_state.State.Id == Guid.Empty)
        {
            _state.State.Id = this.GetPrimaryKey();
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

        // Update state
        _state.State.Status = IngestionOrchestrationState.CopyingFiles;
        await SafeWriteStateAsync();

        // Start the file copy process - pass THIS grain's ID to the file copy grain
        var fileCopyGrain = GrainFactory.GetGrain<IDocumentFileCopyGrain>(this.GetPrimaryKey());
        await fileCopyGrain.CopyFilesFromBlobStorageAsync(
            blobContainerName,
            folderPath,
            _state.State.TargetContainerName,
            documentLibraryShortName,
            documentLibraryType);
    }

    public async Task OnFileCopiedAsync(string fileName, string originalDocumentUrl)
    {
        _state.State.TotalFiles++;
        await SafeWriteStateAsync();

        // Update state if needed
        if (_state.State.Status == IngestionOrchestrationState.CopyingFiles)
        {
            _state.State.Status = IngestionOrchestrationState.ProcessingDocuments;
            await SafeWriteStateAsync();
        }

        try
        {
            // Wait for a slot to become available in the semaphore
            await _processingThrottleSemaphore.WaitAsync(5.Minutes());

            // Start processing the document with a unique grain but pass THIS grain's ID
            // as the orchestration ID to maintain correlation
            var processorGrainId = Guid.NewGuid();
            var processorGrain = GrainFactory.GetGrain<IDocumentProcessorGrain>(processorGrainId);

            // Introduce a half second delay to stagger execution and avoid overwhelming resources
            await Task.Delay(500);

            // Process the file and handle completion/errors
            _ = processorGrain.ProcessDocumentAsync(
                    Path.GetFileName(fileName),
                    originalDocumentUrl,
                    _state.State.DocumentLibraryShortName,
                    _state.State.DocumentLibraryType,
                    this.GetPrimaryKey()) // Pass orchestration grain ID for correlation
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogError(t.Exception,
                            "Error processing document {FileName} for {DocumentLibraryType} {DocumentLibraryName}",
                            fileName, _state.State.DocumentLibraryType, _state.State.DocumentLibraryShortName);
                        
                        // Notify orchestration of failure and release the semaphore
                        _ = OnIngestionFailedAsync($"Failed to process document {fileName}: {t.Exception?.Message}");
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Successfully queued document {FileName} for processing in {DocumentLibraryType} {DocumentLibraryName}",
                            fileName, _state.State.DocumentLibraryType, _state.State.DocumentLibraryShortName);

                        // We don't release the semaphore here - the processor grain will call
                        // OnIngestionCompletedAsync or OnIngestionFailedAsync which will release it
                    }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error queuing document {FileName} for processing in {DocumentLibraryType} {DocumentLibraryName}",
                fileName, _state.State.DocumentLibraryType, _state.State.DocumentLibraryShortName);

            // Notify orchestration of failure and release the semaphore
            await OnIngestionFailedAsync($"Error queuing document {fileName} for processing: {ex.Message}");
        }
    }

    public async Task OnIngestionCompletedAsync()
    {
        _state.State.ProcessedFiles++;
        await SafeWriteStateAsync();

        // Release a slot in the semaphore for the next document
        _processingThrottleSemaphore.Release();

        // If all files are processed (successfully or with errors), mark as complete
        if (_state.State.ProcessedFiles + _state.State.FailedFiles >= _state.State.TotalFiles && _state.State.TotalFiles > 0)
        {
            _state.State.Status = _state.State.FailedFiles > 0 ? IngestionOrchestrationState.Failed : IngestionOrchestrationState.Completed;
            await SafeWriteStateAsync();

            _logger.LogInformation(
                "Document ingestion for {DocumentLibraryName} completed. Processed: {ProcessedFiles}, Failed: {FailedFiles}, Total: {TotalFiles}",
                _state.State.DocumentLibraryShortName, _state.State.ProcessedFiles, _state.State.FailedFiles, _state.State.TotalFiles);
        }
    }

    public async Task OnIngestionFailedAsync(string reason)
    {
        _state.State.FailedFiles++;
        _state.State.Errors.Add(reason);
        await SafeWriteStateAsync();

        // Release a slot in the semaphore for the next document
        _processingThrottleSemaphore.Release();

        // If all files are processed (successfully or with errors), mark as complete
        if (_state.State.ProcessedFiles + _state.State.FailedFiles >= _state.State.TotalFiles && _state.State.TotalFiles > 0)
        {
            _state.State.Status = IngestionOrchestrationState.Failed;
            await SafeWriteStateAsync();

            _logger.LogError(
                "Document ingestion for {DocumentLibraryName} completed with errors. Processed: {ProcessedFiles}, Failed: {FailedFiles}, Total: {TotalFiles}",
                _state.State.DocumentLibraryShortName, _state.State.ProcessedFiles, _state.State.FailedFiles, _state.State.TotalFiles);
        }
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
}
