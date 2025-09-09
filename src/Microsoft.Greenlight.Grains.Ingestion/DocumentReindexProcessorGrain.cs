// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Ingestion;

/// <summary>
/// Processes individual document reindexing operations.
/// </summary>
[Reentrant]
public class DocumentReindexProcessorGrain : Grain, IDocumentReindexProcessorGrain
{
    private readonly ILogger<DocumentReindexProcessorGrain> _logger;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IDocumentIngestionService _documentIngestionService;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;

    private volatile bool _isActive = false;

    public DocumentReindexProcessorGrain(
        ILogger<DocumentReindexProcessorGrain> logger,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IDocumentIngestionService documentIngestionService,
        AzureFileHelper azureFileHelper,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _documentIngestionService = documentIngestionService;
        _azureFileHelper = azureFileHelper;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
    }

    public async Task<bool> IsActiveAsync()
    {
        return _isActive;
    }

    public async Task StartReindexingAsync(Guid ingestedDocumentId, string reason, string orchestrationId)
    {
        if (_isActive)
        {
            _logger.LogWarning("Document {DocumentId} is already being processed", ingestedDocumentId);
            return;
        }

        _isActive = true;

        ConcurrencyLease? lease = null;
        var coordinator = GrainFactory.GetGrain<IGlobalConcurrencyCoordinatorGrain>(ConcurrencyCategory.Ingestion.ToString());

        try
        {
            _logger.LogInformation("Starting reindexing for document {DocumentId}. Reason: {Reason}. Orchestration: {OrchestrationId}",
                ingestedDocumentId, reason, orchestrationId);

            // Acquire cluster-wide ingestion lease
            var requesterId = $"Reindex:{ingestedDocumentId}";
            lease = await coordinator.AcquireAsync(requesterId, weight: 1, waitTimeout: TimeSpan.FromDays(2), leaseTtl: TimeSpan.FromHours(1));

            // Get the document details
            Microsoft.Greenlight.Shared.Models.IngestedDocument document;

            await using (var db = await _dbContextFactory.CreateDbContextAsync())
            {
                document = await db.IngestedDocuments.FirstOrDefaultAsync(d => d.Id == ingestedDocumentId);

                if (document == null)
                {
                    _logger.LogError("Document {DocumentId} not found for reindexing", ingestedDocumentId);
                    return; // Cannot notify orchestration grain without document info
                }
            }

            // Get the orchestration grain using the provided orchestration ID
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentReindexOrchestrationGrain>(orchestrationId);

            // Eligibility: Only ensure the document ingestion is complete. Do not depend on vector store tracking flags,
            // as those will be reset per-document below before reindexing.
            if (document.IngestionState != IngestionState.Complete)
            {
                _logger.LogWarning("Document {DocumentId} not eligible for reindexing (ingestion not complete). Skipping.", ingestedDocumentId);
                await orchestrationGrain.OnReindexFailedAsync("Ingestion not complete", false);
                return;
            }

            // Reset vector-store tracking flags for this document just before reindexing
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var tracked = await db.IngestedDocuments.FirstOrDefaultAsync(d => d.Id == ingestedDocumentId);
                if (tracked != null)
                {
                    tracked.IsVectorStoreIndexed = false;
                    tracked.VectorStoreIndexedDate = null;
                    tracked.VectorStoreChunkCount = 0;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception resetEx)
            {
                _logger.LogWarning(resetEx, "Failed to pre-reset vector store flags for document {DocumentId}. Continuing reindex.", ingestedDocumentId);
            }

            await ProcessDocumentReindexingAsync(document, orchestrationGrain);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during reindexing of document {DocumentId}", ingestedDocumentId);

            // Try to notify the orchestration grain using the provided ID
            try
            {
                var orchestrationGrain = GrainFactory.GetGrain<IDocumentReindexOrchestrationGrain>(orchestrationId);
                await orchestrationGrain.OnReindexFailedAsync($"Unexpected error: {ex.Message}", false);
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, "Failed to notify orchestration grain {OrchestrationId} of error for document {DocumentId}",
                    orchestrationId, ingestedDocumentId);
            }
        }
        finally
        {
            // Release global lease
            if (lease != null)
            {
                try
                {
                    var released = await coordinator.ReleaseAsync(lease.LeaseId);
                    _logger.LogDebug("Released reindex ingestion lease {LeaseId} for document {DocumentId}: {Released}", lease.LeaseId, ingestedDocumentId, released);
                }
                catch (Exception releaseEx)
                {
                    _logger.LogError(releaseEx, "Error releasing reindex ingestion lease for document {DocumentId}", ingestedDocumentId);
                }
            }

            _isActive = false;
        }
    }

    private async Task ProcessDocumentReindexingAsync(
        Microsoft.Greenlight.Shared.Models.IngestedDocument document,
        IDocumentReindexOrchestrationGrain orchestrationGrain)
    {
        try
        {
            // Prefer persisted blob URL to locate the file; auto-import cleanup may have removed source path
            var documentUrl = document.FinalBlobUrl ?? document.OriginalDocumentUrl;
            using var documentStream = await _azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(documentUrl);
            if (documentStream == null)
            {
                _logger.LogError("Unable to open stream for document {DocumentId} at URL {Url}", document.Id, documentUrl);
                await orchestrationGrain.OnReindexFailedAsync($"Source file not found: {documentUrl}", false);
                return;
            }

            // Generate document reference for vector store indexing (identifier instead of URL)
            var documentReference = $"doc:{document.Id}";

            // Determine the correct index name as per ingestion rules
            string indexName;
            if (document.DocumentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
            {
                var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(document.DocumentLibraryOrProcessName);
                if (documentProcess == null)
                {
                    _logger.LogError("Document process {DocumentProcessName} not found", document.DocumentLibraryOrProcessName);
                    await orchestrationGrain.OnReindexFailedAsync($"Document process {document.DocumentLibraryOrProcessName} not found", false);
                    return;
                }
                indexName = documentProcess.Repositories?.FirstOrDefault() ?? document.DocumentLibraryOrProcessName;
            }
            else
            {
                var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(document.DocumentLibraryOrProcessName);
                if (documentLibrary == null)
                {
                    _logger.LogError("Document library {DocumentLibraryName} not found", document.DocumentLibraryOrProcessName);
                    await orchestrationGrain.OnReindexFailedAsync($"Document library {document.DocumentLibraryOrProcessName} not found", false);
                    return;
                }
                indexName = documentLibrary.IndexName;
            }

            // Re-ingest the document using document reference instead of URL
            DocumentIngestionResult result = await _documentIngestionService.IngestDocumentAsync(
                document.Id,
                documentStream,
                document.FileName,
                documentReference,
                document.DocumentLibraryOrProcessName,
                indexName);

            // Update the database based on the result
            await UpdateDocumentStateAsync(document, result, orchestrationGrain);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reindexing of document {DocumentId}", document.Id);

            // Update document with error state
            await UpdateDocumentErrorStateAsync(document.Id, $"Reindexing error: {ex.Message}");

            await orchestrationGrain.OnReindexFailedAsync($"Reindexing error: {ex.Message}", false);
        }
    }

    private async Task UpdateDocumentStateAsync(
        Microsoft.Greenlight.Shared.Models.IngestedDocument document,
        DocumentIngestionResult result,
        IDocumentReindexOrchestrationGrain orchestrationGrain)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            // Re-fetch the document to avoid concurrency issues
            var dbDocument = await db.IngestedDocuments.FirstOrDefaultAsync(d => d.Id == document.Id);
            if (dbDocument == null)
            {
                _logger.LogError("Document {DocumentId} not found in database during state update", document.Id);
                await orchestrationGrain.OnReindexFailedAsync("Document not found during state update", false);
                return;
            }

            if (result.Success)
            {
                var chunkCount = result.ChunkCount;

                // Update the document as successfully reindexed
                dbDocument.IsVectorStoreIndexed = true;
                dbDocument.VectorStoreIndexedDate = DateTime.UtcNow;
                dbDocument.VectorStoreChunkCount = chunkCount;
                dbDocument.Error = null;

                await db.SaveChangesAsync();

                _logger.LogInformation("Successfully reindexed document {DocumentId} with {ChunkCount} chunks",
                    document.Id, chunkCount);

                await orchestrationGrain.OnReindexCompletedAsync();
            }
            else
            {
                var errorMessage = result.ErrorMessage ?? "Unknown error";

                // Update the document with error
                dbDocument.IsVectorStoreIndexed = false;
                dbDocument.Error = $"Reindexing failed: {errorMessage}";

                await db.SaveChangesAsync();

                _logger.LogError("Failed to reindex document {DocumentId}: {Error}", document.Id, errorMessage);
                await orchestrationGrain.OnReindexFailedAsync(errorMessage, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update document {DocumentId} state in database", document.Id);
            await orchestrationGrain.OnReindexFailedAsync($"Database update error: {ex.Message}", false);
        }
    }

    private async Task UpdateDocumentErrorStateAsync(Guid documentId, string errorMessage)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var document = await db.IngestedDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
            if (document != null)
            {
                document.IsVectorStoreIndexed = false;
                document.Error = errorMessage;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception dbEx)
        {
            _logger.LogError(dbEx, "Failed to update document {DocumentId} with error state", documentId);
        }
    }
}