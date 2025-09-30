// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Grains.Shared.Contracts;

namespace Microsoft.Greenlight.Grains.Ingestion
{
    /// <summary>
    /// Grain for tracking ingestion state and progress for a single file.
    /// </summary>
    public class FileIngestionGrain : Grain, IFileIngestionGrain
    {
        private readonly ILogger<FileIngestionGrain> _logger;
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private IngestedDocument? _entity;
        private bool _isInProcess = false;

        // Lease tracking
        private ConcurrencyLease? _lease;

        public FileIngestionGrain(ILogger<FileIngestionGrain> logger, IDbContextFactory<DocGenerationDbContext> dbContextFactory)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var id = this.GetPrimaryKey();
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            _entity = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (_entity == null)
            {
                _logger.LogWarning("IngestedDocument with Id {Id} not found in DB on activation.", id);
            }
            _isInProcess = false;
            await base.OnActivateAsync(cancellationToken);
        }

        /// <summary>
        /// Returns true if this grain is actively processing (in-memory flag, not persisted state).
        /// </summary>
        public Task<bool> IsActiveAsync()
        {
            return Task.FromResult(_isInProcess);
        }

        public async Task StartIngestionAsync(Guid documentId)
        {
            _logger.LogInformation("[StartIngestionAsync] Called for documentId={DocumentId}", documentId);
            await using (var db = await _dbContextFactory.CreateDbContextAsync())
            {
                var entity = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
                if (entity == null)
                {
                    _logger.LogError("Cannot start ingestion: entity with Id {Id} not found in DB.", documentId);
                    return;
                }
                _entity = entity;
            }

            if (_entity == null)
            {
                _logger.LogError("Cannot start ingestion: entity is null after DB lookup.");
                return;
            }

            // Acquire global ingestion lease (weight=1) to enforce cluster-wide concurrency
            var coordinator = GrainFactory.GetGrain<IGlobalConcurrencyCoordinatorGrain>(ConcurrencyCategory.Ingestion.ToString());
            var requesterId = $"Ingest:{_entity.Id}";

            _logger.LogDebug("[StartIngestionAsync] Attempting to acquire ingestion lease for documentId={DocumentId}, fileName={FileName}",
                _entity.Id, _entity.FileName);

            try
            {
                _lease = await coordinator.AcquireAsync(requesterId, weight: 1, waitTimeout: TimeSpan.FromHours(8), leaseTtl: TimeSpan.FromMinutes(90));
                _isInProcess = true;

                _logger.LogDebug("[StartIngestionAsync] Successfully acquired ingestion lease {LeaseId} for documentId={DocumentId}, fileName={FileName}",
                    _lease.LeaseId, _entity.Id, _entity.FileName);
            }
            catch (TimeoutException tex)
            {
                _logger.LogWarning(tex, "Timeout acquiring ingestion lease for document {DocumentId}", _entity.Id);
                var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(_entity.OrchestrationId.ToString());
                await orchestrationGrain.OnIngestionFailedAsync("Timeout waiting for ingestion worker", false);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring ingestion lease for document {DocumentId}", _entity.Id);
                var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(_entity.OrchestrationId.ToString());
                await orchestrationGrain.OnIngestionFailedAsync($"Failed to acquire ingestion worker: {ex.Message}", false);
                return;
            }

            try
            {
                // 1. Copy file if needed
                // Also route DiscoveredForConsumer through the copy grain to honor queueing, but skip physical copy internally
                if (_entity.IngestionState is IngestionState.Discovered or IngestionState.FileCopying or IngestionState.DiscoveredForConsumer)
                {
                    // Set to FileCopying only when actually starting processing
                    _entity.IngestionState = IngestionState.FileCopying;
                    await UpdateEntityStateAsync();
                    var fileCopyGrain = GrainFactory.GetGrain<IDocumentFileCopyGrain>(_entity.Id);
                    _logger.LogDebug("[StartIngestionAsync] Calling CopyFileAsync for documentId={DocumentId}, fileName={FileName}, container={Container}, folderPath={FolderPath}",
                        _entity.Id, _entity.FileName, _entity.Container, _entity.FolderPath);
                    var copyResult = await fileCopyGrain.CopyFileAsync(_entity.Id);

                    _logger.LogDebug("[StartIngestionAsync] CopyFileAsync completed with Success={Success} for documentId={DocumentId}, fileName={FileName}",
                        copyResult.Success, _entity.Id, _entity.FileName);
                    if (copyResult.Success)
                    {
                        await OnFileCopyCompletedAsync();
                    }
                    else
                    {
                        await OnFileCopyFailedAsync(copyResult.Error ?? "Unknown error during file copy.");
                    }
                    return;
                }
                else if (_entity.IngestionState is IngestionState.FileCopied or IngestionState.Processing)
                {
                    // We are probably recovering from a previous state, so just transition to Processing since the file has already been copied

                    _logger.LogDebug("[StartIngestionAsync] File already copied, transitioning to Processing state for documentId={DocumentId}", _entity.Id);
                    await OnFileCopyCompletedAsync();

                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file ingestion process for {FileName} (Id: {Id})", _entity.FileName, _entity.Id);
                _entity.IngestionState = IngestionState.Failed;
                _entity.Error = ex.Message;
                await UpdateEntityStateAsync();
                var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(_entity.OrchestrationId.ToString());
                await orchestrationGrain.OnIngestionFailedAsync($"Failed to ingest file {_entity.FileName}: {ex.Message}", false);
                _isInProcess = false;
                await ReleaseLeaseSafeAsync();
            }
        }

        public async Task OnFileCopyCompletedAsync()
        {
            if (_entity == null) 
            { 
                _isInProcess = false; 
                await ReleaseLeaseSafeAsync(); 
                return; 
            }

            // Update state to Processing
            _entity.IngestionState = IngestionState.Processing;
            await UpdateEntityStateAsync();
            
            _logger.LogDebug("[OnFileCopyCompletedAsync] Starting document processing for documentId={DocumentId}, fileName={FileName}",
                _entity.Id, _entity.FileName);

            try
            {
                // Start processing with explicit timeout and error handling
                var processorGrain = GrainFactory.GetGrain<IDocumentProcessorGrain>(_entity.Id);
                
                // Use a timeout to ensure we don't wait indefinitely
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30)); // 30 minute timeout for processing
                
                DocumentProcessResult processResult;
                try
                {
                    var processingTask = processorGrain.ProcessDocumentAsync(_entity.Id);
                    processResult = await processingTask.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    _logger.LogError("Document processing timed out after 30 minutes for documentId={DocumentId}, fileName={FileName}", 
                        _entity.Id, _entity.FileName);
                    await OnProcessingFailedAsync("Document processing timed out after 30 minutes");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred during document processing for documentId={DocumentId}, fileName={FileName}", 
                        _entity.Id, _entity.FileName);
                    await OnProcessingFailedAsync($"Document processing failed with exception: {ex.Message}");
                    return;
                }

                // Handle the result
                if (processResult.Success)
                {
                    _logger.LogDebug("[OnFileCopyCompletedAsync] Document processing succeeded for documentId={DocumentId}, fileName={FileName}",
                        _entity.Id, _entity.FileName);
                    await OnProcessingCompletedAsync();
                }
                else
                {
                    var errorMessage = processResult.Error ?? "Unknown error during document processing";
                    _logger.LogWarning("[OnFileCopyCompletedAsync] Document processing failed for documentId={DocumentId}, fileName={FileName}, error={Error}", 
                        _entity.Id, _entity.FileName, errorMessage);
                    await OnProcessingFailedAsync(errorMessage);
                }
            }
            catch (Exception ex)
            {
                // Catch-all for any other unexpected exceptions
                _logger.LogError(ex, "Unexpected error in OnFileCopyCompletedAsync for documentId={DocumentId}, fileName={FileName}", 
                    _entity.Id, _entity.FileName);
                await OnProcessingFailedAsync($"Unexpected error during processing setup: {ex.Message}");
            }
        }

        public async Task OnFileCopyFailedAsync(string error)
        {
            _isInProcess = false;
            if (_entity == null) { await ReleaseLeaseSafeAsync(); return; }
            _entity.IngestionState = IngestionState.Failed;
            _entity.Error = error;
            await UpdateEntityStateAsync();
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(_entity.OrchestrationId.ToString());
            await orchestrationGrain.OnIngestionFailedAsync($"File copy failed for {_entity.FileName}: {error}", false);
            await ReleaseLeaseSafeAsync();
        }

        public async Task OnProcessingCompletedAsync()
        {
            _isInProcess = false;
            if (_entity == null) { await ReleaseLeaseSafeAsync(); return; }
            
            _logger.LogInformation("[OnProcessingCompletedAsync] Marking document as complete for documentId={DocumentId}, fileName={FileName}", 
                _entity.Id, _entity.FileName);
            
            _entity.IngestionState = IngestionState.Complete;
            _entity.Error = null;
            await UpdateEntityStateAsync();
            
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(_entity.OrchestrationId.ToString());
            await orchestrationGrain.OnIngestionCompletedAsync();
            await ReleaseLeaseSafeAsync();
        }

        public async Task OnProcessingFailedAsync(string error)
        {
            _isInProcess = false;
            if (_entity == null) { await ReleaseLeaseSafeAsync(); return; }
            
            _logger.LogError("[OnProcessingFailedAsync] Marking document as failed for documentId={DocumentId}, fileName={FileName}, error={Error}", 
                _entity.Id, _entity.FileName, error);
            
            _entity.IngestionState = IngestionState.Failed;
            _entity.Error = error;
            await UpdateEntityStateAsync();
            
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(_entity.OrchestrationId.ToString());
            await orchestrationGrain.OnIngestionFailedAsync($"Processing failed for {_entity.FileName}: {error}", false);
            await ReleaseLeaseSafeAsync();
        }

        public async Task UpdateStateAsync(Guid documentId)
        {
            await using var db = _dbContextFactory.CreateDbContext();
            var entity = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
            if (entity != null)
            {
                _entity = entity;
                _logger.LogInformation("Updated ingestion state for file {FileName} (Id: {Id}) to {State}", entity.FileName, entity.Id, entity.IngestionState);
            }
        }

        public async Task<Guid> GetStateIdAsync()
        {
            if (_entity == null)
                throw new InvalidOperationException("File ingestion state not initialized.");
            return await Task.FromResult(_entity.Id);
        }

        public async Task MarkCompleteAsync()
        {
            _isInProcess = false;
            if (_entity != null)
            {
                await using var db = _dbContextFactory.CreateDbContext();
                var entity = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == _entity.Id);
                if (entity != null)
                {
                    entity.IngestionState = IngestionState.Complete;
                    entity.Error = null;
                    await db.SaveChangesAsync();
                    _entity = entity;
                    _logger.LogInformation("File ingestion complete for {FileName} (Id: {Id})", entity.FileName, entity.Id);
                }
            }
            await ReleaseLeaseSafeAsync();
        }

        public async Task MarkFailedAsync(string error)
        {
            _isInProcess = false;
            if (_entity != null)
            {
                await using var db = _dbContextFactory.CreateDbContext();
                var entity = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == _entity.Id);
                if (entity != null)
                {
                    entity.IngestionState = IngestionState.Failed;
                    entity.Error = error;
                    await db.SaveChangesAsync();
                    _entity = entity;
                    _logger.LogError("File ingestion failed for {FileName} (Id: {Id}): {Error}", entity.FileName, entity.Id, error);
                }
            }
            await ReleaseLeaseSafeAsync();
        }

        private async Task UpdateEntityStateAsync()
        {
            if (_entity == null) return;
            
            try
            {
                await using var db = _dbContextFactory.CreateDbContext();
                var entity = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == _entity.Id);
                if (entity != null)
                {
                    entity.IngestionState = _entity.IngestionState;
                    entity.Error = _entity.Error;
                    await db.SaveChangesAsync();
                    _logger.LogDebug("Updated entity state for documentId={DocumentId} to {State}", _entity.Id, _entity.IngestionState);
                }
                else
                {
                    _logger.LogWarning("Could not find entity with Id {Id} to update state", _entity.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update entity state for documentId={DocumentId}", _entity.Id);
                // Don't throw here as this would break the ingestion flow
            }
        }

        private async Task ReleaseLeaseSafeAsync()
        {
            if (_lease == null) return;
            try
            {
                var coordinator = GrainFactory.GetGrain<IGlobalConcurrencyCoordinatorGrain>(ConcurrencyCategory.Ingestion.ToString());
                var released = await coordinator.ReleaseAsync(_lease.LeaseId);

                if (released)
                {
                    _logger.LogDebug("Released ingestion lease {LeaseId} for document {DocumentId}", _lease.LeaseId, _entity?.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to release lease {LeaseId} for document {DocumentId} - coordinator may have been reactivated, lease was already orphaned",
                        _lease.LeaseId, _entity?.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing ingestion lease for document {DocumentId}", _entity?.Id);
            }
            finally
            {
                _lease = null;
            }
        }
    }
}
