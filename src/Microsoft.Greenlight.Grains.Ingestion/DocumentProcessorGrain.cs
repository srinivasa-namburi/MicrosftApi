// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Ingestion;

[Reentrant]
public class DocumentProcessorGrain : Grain, IDocumentProcessorGrain
{
    private readonly ILogger<DocumentProcessorGrain> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IDocumentIngestionService _documentIngestionService;
    private readonly IFileUrlResolverService _fileUrlResolverService;
    private bool _isRunning; // In-memory, not persisted

    public DocumentProcessorGrain(
        ILogger<DocumentProcessorGrain> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IServiceProvider serviceProvider,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IDocumentIngestionService documentIngestionService,
        IFileUrlResolverService fileUrlResolverService)
    {
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;
        _documentIngestionService = documentIngestionService;
        _fileUrlResolverService = fileUrlResolverService;
    }

    public async Task<DocumentProcessResult> ProcessDocumentAsync(Guid documentId)
    {
        if (_isRunning)
        {
            _logger.LogInformation("Processing already running for file {FileId}, skipping.", documentId);
            return DocumentProcessResult.Fail("Processing already running.");
        }
        _isRunning = true;

        try
        {
            // IMPORTANT: Do NOT acquire the global ingestion lease here.
            // FileIngestionGrain holds the cluster-wide ingestion lease while calling into this grain.
            // Acquiring it again here caused a deadlock. Concurrency is already controlled by the caller.

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var entity = await db.IngestedDocuments
                .Include(d => d.IngestedDocumentFileAcknowledgments)
                    .ThenInclude(idfa => idfa.FileAcknowledgmentRecord)
                        .ThenInclude(far => far.FileStorageSource)
                            .ThenInclude(fss => fss.FileStorageHost)
                .FirstOrDefaultAsync(d => d.Id == documentId);
                
            if (entity == null)
            {
                _logger.LogError("IngestedDocument with Id {Id} not found in DB for processing.", documentId);
                return DocumentProcessResult.Fail($"IngestedDocument with Id {documentId} not found in DB.");
            }

            string fileName = entity.FileName;
            string documentLibraryShortName = entity.DocumentLibraryOrProcessName ?? string.Empty;
            DocumentLibraryType documentLibraryType = entity.DocumentLibraryType;
            string? uploadedByUserOid = entity.UploadedByUserOid;

            // Generate document reference for vector store (identifier instead of URL)
            string documentReference = GenerateDocumentReference(entity);

            _logger.LogInformation("Starting document processing for {FileName} (Id: {DocumentId})", fileName, documentId);

            // Track vector store processing start
            entity.IngestionState = IngestionState.Processing;
            await db.SaveChangesAsync();

            string indexName;
            // Use canonical vector document id scheme: Base64UrlEncode of sanitized filename
            string vectorStoreDocumentId = Base64UrlEncode(SanitizeFileName(fileName));

            if (documentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
            {
                var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryShortName);
                if (documentProcess == null)
                {
                    _logger.LogError("Document process {DocumentProcessName} not found", documentLibraryShortName);
                    await UpdateIngestionFailureAsync(entity, $"Document process {documentLibraryShortName} not found");
                    return DocumentProcessResult.Fail($"Document process {documentLibraryShortName} not found");
                }
                indexName = documentProcess.Repositories.FirstOrDefault() ?? documentLibraryShortName;
            }
            else // AdditionalDocumentLibrary
            {
                using var scope = _serviceProvider.CreateScope();
                var documentLibraryInfoService = scope.ServiceProvider.GetRequiredService<IDocumentLibraryInfoService>();
                var documentLibrary = await documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);
                if (documentLibrary == null)
                {
                    _logger.LogError("Document library {DocumentLibraryName} not found", documentLibraryShortName);
                    await UpdateIngestionFailureAsync(entity, $"Document library {documentLibraryShortName} not found");
                    return DocumentProcessResult.Fail($"Document library {documentLibraryShortName} not found");
                }
                indexName = documentLibrary.IndexName;
            }

            await using var fileStream = await GetDocumentStreamAsync(documentId);
            if (fileStream == null)
            {
                _logger.LogError("Failed to get document stream for document {DocumentId}", documentId);
                await UpdateIngestionFailureAsync(entity, $"Failed to get document stream for document {documentId}");
                return DocumentProcessResult.Fail($"Failed to get document stream for document {documentId}");
            }

            _logger.LogInformation("Starting document ingestion for {FileName} with indexName={IndexName}", fileName, indexName);

            // Use the document ingestion service with document reference instead of URL
            DocumentIngestionResult result;
            try
            {
                result = await _documentIngestionService.IngestDocumentAsync(
                    documentId,
                    fileStream,
                    fileName,
                    documentReference, // Use document reference instead of URL
                    documentLibraryShortName,
                    indexName,
                    uploadedByUserOid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during document ingestion service call for {FileName} (Id: {DocumentId})", fileName, documentId);
                await UpdateIngestionFailureAsync(entity, $"Document ingestion service failed: {ex.Message}");
                return DocumentProcessResult.Fail($"Document ingestion service failed: {ex.Message}");
            }

            if (!result.Success)
            {
                _logger.LogError("Failed to ingest document {DocumentId}: {Error}", documentId, result.ErrorMessage);
                await UpdateIngestionFailureAsync(entity, result.ErrorMessage ?? "Document ingestion failed");
                return DocumentProcessResult.Fail(result.ErrorMessage ?? "Document ingestion failed");
            }

            // Update IngestedDocument with vector store tracking information
            await UpdateIngestionSuccessAsync(entity, vectorStoreDocumentId, indexName, result.ChunkCount);

            _logger.LogInformation("Successfully processed document {FileName} for {DocumentLibraryType} {DocumentLibraryName} - {ChunkCount} chunks indexed",
                fileName, documentLibraryType, documentLibraryShortName, result.ChunkCount);
            return DocumentProcessResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document processing for {DocumentId}", documentId);

            // Fallback: Update entity state if possible
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var entity = await db.IngestedDocuments.FindAsync(documentId);
                if (entity != null)
                {
                    await UpdateIngestionFailureAsync(entity, $"Unexpected error during processing: {ex.Message}");
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to update document state after processing failure for {DocumentId}", documentId);
            }

            return DocumentProcessResult.Fail($"Unexpected error during processing: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// Updates the IngestedDocument entity after successful vector store ingestion.
    /// Includes fallback logic to ensure critical operations don't fail due to tracking updates.
    /// </summary>
    /// <param name="entity">The IngestedDocument entity to update.</param>
    /// <param name="vectorStoreDocumentId">The document ID used in the vector store.</param>
    /// <param name="indexName">The vector store index name.</param>
    /// <param name="chunkCount">Number of chunks created.</param>
    private async Task UpdateIngestionSuccessAsync(
        Microsoft.Greenlight.Shared.Models.IngestedDocument entity,
        string vectorStoreDocumentId,
        string indexName,
        int chunkCount)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var trackedEntity = await db.IngestedDocuments.FindAsync(entity.Id);
            
            if (trackedEntity != null)
            {
                // Update core ingestion state
                trackedEntity.IngestionState = IngestionState.Complete;
                trackedEntity.Error = null;

                // Update vector store tracking properties with fallback values
                trackedEntity.VectorStoreDocumentId = !string.IsNullOrWhiteSpace(vectorStoreDocumentId) 
                    ? vectorStoreDocumentId 
                    : Base64UrlEncode(SanitizeFileName(trackedEntity.FileName)); // Fallback to canonical scheme
                
                trackedEntity.VectorStoreIndexName = !string.IsNullOrWhiteSpace(indexName) 
                    ? indexName 
                    : trackedEntity.DocumentLibraryOrProcessName; // Fallback to library name
                
                trackedEntity.VectorStoreChunkCount = chunkCount > 0 ? chunkCount : 0; // Fallback to 0 if invalid
                trackedEntity.VectorStoreIndexedDate = DateTime.UtcNow;
                trackedEntity.IsVectorStoreIndexed = true;

                await db.SaveChangesAsync();

                _logger.LogDebug("Updated vector store tracking for document {DocumentId}: DocumentId={VectorStoreDocumentId}, Index={IndexName}, Chunks={ChunkCount}",
                    entity.Id, trackedEntity.VectorStoreDocumentId, trackedEntity.VectorStoreIndexName, trackedEntity.VectorStoreChunkCount);
            }
            else
            {
                _logger.LogWarning("Could not find IngestedDocument {DocumentId} to update vector store tracking", entity.Id);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the entire ingestion process due to tracking update failures
            _logger.LogWarning(ex, "Failed to update vector store tracking for document {DocumentId}, but ingestion succeeded", entity.Id);
        }
    }

    /// <summary>
    /// Updates the IngestedDocument entity after ingestion failure.
    /// Includes fallback logic to ensure state updates don't fail silently.
    /// </summary>
    /// <param name="entity">The IngestedDocument entity to update.</param>
    /// <param name="errorMessage">The error message to record.</param>
    private async Task UpdateIngestionFailureAsync(
        Microsoft.Greenlight.Shared.Models.IngestedDocument entity,
        string errorMessage)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var trackedEntity = await db.IngestedDocuments.FindAsync(entity.Id);
            
            if (trackedEntity != null)
            {
                trackedEntity.IngestionState = IngestionState.Failed;
                trackedEntity.Error = !string.IsNullOrWhiteSpace(errorMessage) 
                    ? errorMessage 
                    : "Unknown ingestion error"; // Fallback error message
                
                // Reset vector store tracking on failure
                trackedEntity.IsVectorStoreIndexed = false;
                trackedEntity.VectorStoreIndexedDate = null;

                await db.SaveChangesAsync();

                _logger.LogDebug("Updated ingestion failure state for document {DocumentId}: {ErrorMessage}", entity.Id, errorMessage);
            }
            else
            {
                _logger.LogWarning("Could not find IngestedDocument {DocumentId} to update failure state", entity.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update ingestion failure state for document {DocumentId}", entity.Id);
        }
    }

    /// <summary>
    /// Gets the document stream with fallback logic and proper error handling.
    /// Supports both blob storage URLs and FileStorageSource-based files.
    /// </summary>
    /// <param name="documentId">The document ID to retrieve stream for.</param>
    /// <returns>Document stream or null if failed.</returns>
    private async Task<Stream?> GetDocumentStreamAsync(Guid documentId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            // Load the document with its file acknowledgments
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var document = await db.IngestedDocuments
                .Include(d => d.IngestedDocumentFileAcknowledgments)
                    .ThenInclude(idfa => idfa.FileAcknowledgmentRecord)
                        .ThenInclude(far => far.FileStorageSource)
                            .ThenInclude(fss => fss.FileStorageHost)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                _logger.LogWarning("IngestedDocument {DocumentId} not found", documentId);
                return null;
            }

            if (document.IngestedDocumentFileAcknowledgments.Any())
            {
                // This document is associated with a FileStorageSource - use the appropriate service
                var acknowledgment = document.IngestedDocumentFileAcknowledgments.First();
                var fileStorageSource = acknowledgment.FileAcknowledgmentRecord.FileStorageSource;
                
                if (fileStorageSource?.FileStorageHost != null)
                {
                    var fileStorageServiceFactory = scope.ServiceProvider.GetRequiredService<IFileStorageServiceFactory>();
                    var fileStorageService = await fileStorageServiceFactory.GetServiceBySourceIdAsync(fileStorageSource.Id);
                    
                    if (fileStorageService != null)
                    {
                        _logger.LogDebug("Using FileStorageService {ProviderType} to retrieve document stream for document {DocumentId}",
                            fileStorageService.ProviderType, documentId);
                        
                        var relativePath = acknowledgment.FileAcknowledgmentRecord.RelativeFilePath;
                        try
                        {
                            // Primary path via provider using relative path
                            return await fileStorageService.GetFileStreamAsync(relativePath);
                        }
                        catch (Exception ex)
                        {
                            // Fallback: if the stored relative path is stale (e.g., moved), try full URL
                            var fallbackUrl = document.FinalBlobUrl ?? acknowledgment.FileAcknowledgmentRecord.FileStorageSourceInternalUrl;
                            if (!string.IsNullOrWhiteSpace(fallbackUrl))
                            {
                                _logger.LogWarning(ex, "Primary storage read failed for {RelativePath}. Falling back to full URL for document {DocumentId}.", relativePath, documentId);
                                var azureFileHelper = scope.ServiceProvider.GetRequiredService<AzureFileHelper>();
                                return await azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(fallbackUrl);
                            }

                            _logger.LogWarning(ex, "Primary storage read failed and no fallback URL available for document {DocumentId}", documentId);
                            return null;
                        }
                    }
                }
            }

            // Fallback to legacy blob storage approach using the document's URL
            var documentUrl = document.FinalBlobUrl ?? document.OriginalDocumentUrl;
            if (!string.IsNullOrWhiteSpace(documentUrl))
            {
                var azureFileHelper = scope.ServiceProvider.GetRequiredService<AzureFileHelper>();
                return await azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(documentUrl);
            }

            _logger.LogWarning("No valid URL found for document {DocumentId}", documentId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document stream for document {DocumentId}", documentId);
            return null;
        }
    }

    /// <summary>
    /// Sanitizes a file name for use as a vector store document ID.
    /// Provides consistent sanitization logic with fallback.
    /// </summary>
    /// <param name="fileName">The original file name.</param>
    /// <returns>Sanitized file name suitable for vector store document ID.</returns>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return $"unknown_file_{Guid.NewGuid():N}"; // Fallback for missing filename
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

    /// <summary>
    /// URL-safe Base64 encode without padding for consistent, provider-friendly identifiers.
    /// Mirrors the repository implementation to keep IDs aligned.
    /// </summary>
    private static string Base64UrlEncode(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Generates a document reference identifier for vector store indexing.
    /// This identifier will be used to dynamically resolve URLs at search time.
    /// </summary>
    /// <param name="entity">The IngestedDocument entity.</param>
    /// <returns>Document reference identifier.</returns>
    private static string GenerateDocumentReference(Microsoft.Greenlight.Shared.Models.IngestedDocument entity)
    {
        // Use the document ID as the primary reference
        // This can be resolved back to the IngestedDocument for URL generation
        return $"doc:{entity.Id}";
    }
}
