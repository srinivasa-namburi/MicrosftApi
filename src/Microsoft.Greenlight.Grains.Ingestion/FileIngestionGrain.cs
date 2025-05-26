// Copyright (c) Microsoft Corporation. All rights reserved.
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

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

            _isInProcess = true;
            try
            {
                // 1. Copy file if needed
                if (_entity.IngestionState is IngestionState.Discovered or IngestionState.FileCopying)
                {
                    // Set to FileCopying only when actually starting processing
                    _entity.IngestionState = IngestionState.FileCopying;
                    await UpdateEntityStateAsync();
                    var fileCopyGrain = GrainFactory.GetGrain<IDocumentFileCopyGrain>(_entity.Id);
                    _logger.LogInformation("[StartIngestionAsync] Calling CopyFileAsync for documentId={DocumentId}, container={Container}, folderPath={FolderPath}", _entity.Id, _entity.Container, _entity.FolderPath);
                    var copyResult = await fileCopyGrain.CopyFileAsync(_entity.Id);
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

                    _logger.LogInformation("[StartIngestionAsync] File already copied, transitioning to Processing state for documentId={DocumentId}", _entity.Id);
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
            }
        }

        public async Task OnFileCopyCompletedAsync()
        {
            if (_entity == null) { _isInProcess = false; return; }
            _entity.IngestionState = IngestionState.Processing;
            await UpdateEntityStateAsync();
            // Start processing and await result
            var processorGrain = GrainFactory.GetGrain<IDocumentProcessorGrain>(_entity.Id);
            var processResult = await processorGrain.ProcessDocumentAsync(_entity.Id);
            if (processResult.Success)
            {
                await OnProcessingCompletedAsync();
            }
            else
            {
                await OnProcessingFailedAsync(processResult.Error ?? "Unknown error during document processing.");
            }
        }

        public async Task OnFileCopyFailedAsync(string error)
        {
            _isInProcess = false;
            if (_entity == null) return;
            _entity.IngestionState = IngestionState.Failed;
            _entity.Error = error;
            await UpdateEntityStateAsync();
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(_entity.OrchestrationId.ToString());
            await orchestrationGrain.OnIngestionFailedAsync($"File copy failed for {_entity.FileName}: {error}", false);
        }

        public async Task OnProcessingCompletedAsync()
        {
            _isInProcess = false;
            if (_entity == null) return;
            _entity.IngestionState = IngestionState.Complete;
            _entity.Error = null;
            await UpdateEntityStateAsync();
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(_entity.OrchestrationId.ToString());
            await orchestrationGrain.OnIngestionCompletedAsync();
        }

        public async Task OnProcessingFailedAsync(string error)
        {
            _isInProcess = false;
            if (_entity == null) return;
            _entity.IngestionState = IngestionState.Failed;
            _entity.Error = error;
            await UpdateEntityStateAsync();
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(_entity.OrchestrationId.ToString());
            await orchestrationGrain.OnIngestionFailedAsync($"Processing failed for {_entity.FileName}: {error}", false);
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
        }

        private async Task UpdateEntityStateAsync()
        {
            if (_entity == null) return;
            await using var db = _dbContextFactory.CreateDbContext();
            var entity = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == _entity.Id);
            if (entity != null)
            {
                entity.IngestionState = _entity.IngestionState;
                entity.Error = _entity.Error;
                await db.SaveChangesAsync();
            }
        }
    }
}
