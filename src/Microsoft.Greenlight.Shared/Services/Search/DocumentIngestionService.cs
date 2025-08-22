// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Default implementation of document ingestion service that supports both KM and SK Vector Store.
/// </summary>
public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDocumentRepositoryFactory _repositoryFactory;
    private readonly ITextExtractionService _textExtractionService;
    private readonly ITextChunkingService _textChunkingService;
    private readonly ILogger<DocumentIngestionService> _logger;
    private readonly VectorStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentIngestionService"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider.</param>
    /// <param name="repositoryFactory">Repository factory.</param>
    /// <param name="textExtractionService">Text extraction service.</param>
    /// <param name="textChunkingService">Text chunking service.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="rootOptions">Root service configuration options snapshot used to access vector store settings.</param>
    public DocumentIngestionService(
        IServiceProvider serviceProvider,
        IDocumentRepositoryFactory repositoryFactory,
        ITextExtractionService textExtractionService,
        ITextChunkingService textChunkingService,
        ILogger<DocumentIngestionService> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> rootOptions)
    {
        _serviceProvider = serviceProvider;
        _repositoryFactory = repositoryFactory;
        _textExtractionService = textExtractionService;
        _textChunkingService = textChunkingService;
        _logger = logger;
        _options = rootOptions.Value.GreenlightServices.VectorStore;
    }

    /// <inheritdoc />
    public async Task<DocumentIngestionResult> IngestDocumentAsync(
        Guid documentId,
        Stream fileStream,
        string fileName,
        string documentUrl,
        string documentLibraryName,
        string indexName,
        string? userId = null,
        Dictionary<string, string>? additionalTags = null)
    {
        try
        {
            _logger.LogInformation("Starting ingestion of document {FileName} for library {DocumentLibraryName}",
                fileName, documentLibraryName);

            // Check if file type is supported
            if (!_textExtractionService.SupportsFileType(fileName))
            {
                var errorMessage = $"Unsupported file type for {fileName}";
                _logger.LogWarning(errorMessage);
                return DocumentIngestionResult.Fail(errorMessage);
            }

            // Extract text content
            var extractedText = await _textExtractionService.ExtractTextAsync(fileStream, fileName);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                var errorMessage = $"No text content could be extracted from {fileName}";
                _logger.LogWarning(errorMessage);
                return DocumentIngestionResult.Fail(errorMessage);
            }

            // Get appropriate repository
            var repository = await GetRepositoryAsync(documentLibraryName);

            // Reset stream position for repository
            fileStream.Position = 0;

            // Store the document using repository (which will handle chunking)
            await repository.StoreContentAsync(
                documentLibraryName,
                indexName,
                fileStream,
                fileName,
                documentUrl,
                userId,
                additionalTags);

            var documentSizeBytes = fileStream.Length;
            var chunkCount = await EstimateChunkCountAsync(extractedText, documentLibraryName);

            _logger.LogInformation("Successfully ingested document {FileName} ({SizeBytes} bytes, ~{ChunkCount} chunks)",
                fileName, documentSizeBytes, chunkCount);

            return DocumentIngestionResult.Ok(chunkCount, documentSizeBytes);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to ingest document {fileName}: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return DocumentIngestionResult.Fail(errorMessage);
        }
    }

    /// <inheritdoc />
    public async Task<DocumentIngestionResult> DeleteDocumentAsync(
        string documentLibraryName,
        string indexName,
        string fileName)
    {
        try
        {
            _logger.LogInformation("Deleting document {FileName} from library {DocumentLibraryName}",
                fileName, documentLibraryName);

            var repository = await GetRepositoryAsync(documentLibraryName);
            await repository.DeleteContentAsync(documentLibraryName, indexName, fileName);

            _logger.LogInformation("Successfully deleted document {FileName}", fileName);
            return DocumentIngestionResult.Ok();
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to delete document {fileName}: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return DocumentIngestionResult.Fail(errorMessage);
        }
    }

    /// <inheritdoc />
    public async Task ClearIndexAsync(string documentLibraryName, string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Clearing vector index {IndexName} for context {Context}", indexName, documentLibraryName);
            var provider = _serviceProvider.GetService<ISemanticKernelVectorStoreProvider>();
            if (provider == null)
            {
                _logger.LogWarning("Vector store provider not available; cannot clear collection {IndexName}", indexName);
                return;
            }

            await provider.ClearCollectionAsync(indexName, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Cleared vector index {IndexName}", indexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear vector index {IndexName}", indexName);
            throw;
        }
    }

    private async Task<IDocumentRepository> GetRepositoryAsync(string documentLibraryName)
    {
        // Try to get document process first
        var documentProcessInfoService = _serviceProvider.GetRequiredService<IDocumentProcessInfoService>();
        var documentProcess = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryName);

        if (documentProcess != null)
        {
            return await _repositoryFactory.CreateForDocumentProcessAsync(documentProcess);
        }

        // Otherwise, treat as document library
        return await _repositoryFactory.CreateForDocumentLibraryAsync(documentLibraryName);
    }

    private async Task<int> EstimateChunkCountAsync(string text, string documentLibraryName)
    {
        // Get chunk size configuration for the specific document process/library
        var chunkSize = await GetEffectiveChunkSizeAsync(documentLibraryName);
        var estimatedTokens = _textChunkingService.EstimateTokenCount(text);
        return (int)Math.Ceiling((double)estimatedTokens / chunkSize);
    }

    private async Task<int> GetEffectiveChunkSizeAsync(string documentLibraryName)
    {
        try
        {
            // Try to get document process-specific chunk size
            var documentProcessInfoService = _serviceProvider.GetRequiredService<IDocumentProcessInfoService>();
            var documentProcess = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryName);

            if (documentProcess?.LogicType == DocumentProcessLogicType.SemanticKernelVectorStore &&
                documentProcess.VectorStoreChunkSize.HasValue)
            {
                _logger.LogDebug("Using document process-specific chunk size {ChunkSize} for {DocumentLibraryName}",
                    documentProcess.VectorStoreChunkSize.Value, documentLibraryName);
                return documentProcess.VectorStoreChunkSize.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get document process info for {DocumentLibraryName}, using default chunk size",
                documentLibraryName);
        }

        // Fall back to global configuration
        var fallbackChunkSize = _options.ChunkSize > 0 ? _options.ChunkSize : 1000;
        _logger.LogDebug("Using global chunk size {ChunkSize} for {DocumentLibraryName}",
            fallbackChunkSize, documentLibraryName);
        return fallbackChunkSize;
    }
}
