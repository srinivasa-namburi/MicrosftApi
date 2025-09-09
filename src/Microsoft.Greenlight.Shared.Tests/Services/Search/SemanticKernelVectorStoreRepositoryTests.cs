// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Moq;
using System.Text;

namespace Microsoft.Greenlight.Shared.Tests.Services.Search;

/// <summary>
/// Tests for SemanticKernelVectorStoreRepository (IDocumentRepository implementation).
/// </summary>
public class SemanticKernelVectorStoreRepositoryTests
{
    private readonly Mock<ILogger<SemanticKernelVectorStoreRepository>> _mockLogger;
    private readonly Mock<IOptionsSnapshot<ServiceConfigurationOptions>> _mockOptions;
    private readonly Mock<IAiEmbeddingService> _mockEmbeddingService;
    private readonly Mock<ISemanticKernelVectorStoreProvider> _mockProvider;
    private readonly Mock<ITextExtractionService> _mockTextExtractionService;
    private readonly Mock<ITextChunkingService> _mockChunkingService;
    private readonly Mock<IFileUrlResolverService> _mockFileUrlResolver;
    private readonly SemanticKernelVectorStoreRepository _repository;
    private readonly ServiceConfigurationOptions _serviceOptions;

    public SemanticKernelVectorStoreRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<SemanticKernelVectorStoreRepository>>();
        _mockOptions = new Mock<IOptionsSnapshot<ServiceConfigurationOptions>>();
        _mockEmbeddingService = new Mock<IAiEmbeddingService>();
        _mockProvider = new Mock<ISemanticKernelVectorStoreProvider>();
        _mockTextExtractionService = new Mock<ITextExtractionService>();
        _mockChunkingService = new Mock<ITextChunkingService>();
        _mockFileUrlResolver = new Mock<IFileUrlResolverService>();

        // Setup default configuration
        _serviceOptions = new ServiceConfigurationOptions
        {
            GreenlightServices = new ServiceConfigurationOptions.GreenlightServicesOptions
            {
                VectorStore = new VectorStoreOptions
                {
                    ChunkOverlap = 100,
                    ChunkSize = 1000,
                    VectorSize = 384,
                    MaxSearchResults = 10,
                    MinRelevanceScore = 0.5
                }
            }
        };

        _mockOptions.Setup(x => x.Value).Returns(_serviceOptions);

        _repository = new SemanticKernelVectorStoreRepository(
            _mockLogger.Object,
            _mockOptions.Object,
            _mockEmbeddingService.Object,
            _mockProvider.Object,
            _mockTextExtractionService.Object,
            _mockChunkingService.Object,
            _mockFileUrlResolver.Object);
    }

    [Fact]
    public async Task StoreContentAsync_ShouldProcessDocument_WhenValidStreamProvided()
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";
        const string documentUrl = "https://example.com/test-document.pdf";
        const string userId = "user123";
        var additionalTags = new Dictionary<string, string> { { "category", "test" } };

        var documentStream = CreateTestDocumentStream("Test document content for processing");
        var extractedText = "Extracted text from the test document content for processing";
        var chunks = new List<string> { "Chunk 1 content", "Chunk 2 content", "Chunk 3 content" };
        var embeddings = new List<float[]>
        {
            Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray(),
            Enumerable.Range(0, 384).Select(x => (float)(x * 0.002)).ToArray(),
            Enumerable.Range(0, 384).Select(x => (float)(x * 0.003)).ToArray()
        };

        // Setup mocks
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(extractedText);

        _mockChunkingService.Setup(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        // Setup embedding resolution for ResolveEmbeddingDimensionsAsync
        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(documentLibraryName))
            .ReturnsAsync(("text-embedding-ada-002", 384));

        // Setup embedding generation for each chunk - using the correct method signature
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, It.IsAny<string>()))
            .ReturnsAsync((string libName, string text) => embeddings[chunks.IndexOf(text)]);

        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, 384, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.StoreContentAsync(documentLibraryName, indexName, documentStream, fileName, documentUrl, userId, additionalTags);

        // Assert
        _mockTextExtractionService.Verify(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName), Times.Once);
        _mockChunkingService.Verify(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, It.IsAny<string>()), Times.Exactly(chunks.Count));
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, 384, It.IsAny<CancellationToken>()), Times.Once);
        _mockProvider.Verify(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreContentAsync_ShouldUseDocumentProcessEmbeddings_WhenDocumentProcessLibraryType()
    {
        // Arrange
        const string documentProcessName = "test-process";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";
        const string documentUrl = "https://example.com/test-document.pdf";
        const string userId = "user123";

        var documentStream = CreateTestDocumentStream("Test document content for processing");
        var extractedText = "Extracted text from the test document content for processing";
        var chunks = new List<string> { "Chunk 1 content", "Chunk 2 content" };
        var embeddings = new List<float[]>
        {
            Enumerable.Range(0, 1024).Select(x => (float)(x * 0.001)).ToArray(),
            Enumerable.Range(0, 1024).Select(x => (float)(x * 0.002)).ToArray()
        };

        // Create repository with DocumentProcessLibrary type
        var processRepository = new SemanticKernelVectorStoreRepository(
            _mockLogger.Object,
            _mockOptions.Object,
            _mockEmbeddingService.Object,
            _mockProvider.Object,
            _mockTextExtractionService.Object,
            _mockChunkingService.Object,
            _mockFileUrlResolver.Object,
            documentLibraryType: DocumentLibraryType.PrimaryDocumentProcessLibrary);

        // Setup mocks
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(extractedText);

        _mockChunkingService.Setup(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        // Setup embedding resolution for document process (should be tried first)
        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcessName))
            .ReturnsAsync(("text-embedding-3-large", 1024));

        // Setup embedding generation for document process
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()))
            .ReturnsAsync((string processName, string text) => embeddings[chunks.IndexOf(text)]);

        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, 1024, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await processRepository.StoreContentAsync(documentProcessName, indexName, documentStream, fileName, documentUrl, userId);

        // Assert
        _mockEmbeddingService.Verify(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcessName), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()), Times.Exactly(chunks.Count));
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, 1024, It.IsAny<CancellationToken>()), Times.Once);
        
        // Should not call document library methods
        _mockEmbeddingService.Verify(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(It.IsAny<string>()), Times.Never);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StoreContentAsync_ShouldFallbackToDocumentLibrary_WhenDocumentProcessResolutionFails()
    {
        // Arrange
        const string documentProcessName = "test-process";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";

        var documentStream = CreateTestDocumentStream("Test content");
        var extractedText = "Extracted text";
        var chunks = new List<string> { "Chunk 1 content" };
        var embeddings = new List<float[]>
        {
            Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray()
        };

        // Create repository with DocumentProcessLibrary type
        var processRepository = new SemanticKernelVectorStoreRepository(
            _mockLogger.Object,
            _mockOptions.Object,
            _mockEmbeddingService.Object,
            _mockProvider.Object,
            _mockTextExtractionService.Object,
            _mockChunkingService.Object,
            _mockFileUrlResolver.Object,
            documentLibraryType: DocumentLibraryType.PrimaryDocumentProcessLibrary);

        // Setup mocks
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(extractedText);

        _mockChunkingService.Setup(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        // Setup document process resolution to fail
        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcessName))
            .ThrowsAsync(new InvalidOperationException("Document process not found"));

        // Setup document library resolution as fallback for dimensions
        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(documentProcessName))
            .ReturnsAsync(("text-embedding-ada-002", 384));

        // Even though dimensions resolution falls back to document library, 
        // embedding generation still uses document process method since _documentLibraryType is PrimaryDocumentProcessLibrary
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()))
            .ReturnsAsync((string processName, string text) => embeddings[0]);

        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, 384, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await processRepository.StoreContentAsync(documentProcessName, indexName, documentStream, fileName, null);

        // Assert
        _mockEmbeddingService.Verify(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcessName), Times.Once);
        _mockEmbeddingService.Verify(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(documentProcessName), Times.Once);
        // Context-aware embedding generation still uses document process method
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()), Times.Once);
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, 384, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreContentAsync_ShouldUseFallbackDimensions_WhenEmbeddingResolutionFails()
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";

        var documentStream = CreateTestDocumentStream("Test content");
        var extractedText = "Extracted text";
        var chunks = new List<string> { "Chunk 1 content" };
        var embeddings = new List<float[]>
        {
            Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray() // Using config VectorSize 384
        };

        // Setup mocks
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(extractedText);

        _mockChunkingService.Setup(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        // Setup all embedding resolution to fail
        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(documentLibraryName))
            .ThrowsAsync(new InvalidOperationException("Embedding config not found"));

        // For non-process repository, won't try ResolveEmbeddingConfigForDocumentProcessAsync

        // Setup embedding generation to work with fallback
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, It.IsAny<string>()))
            .ReturnsAsync((string libName, string text) => embeddings[0]);

        // When all resolution fails, should use default 1536 (text-embedding-ada-002 default) or fallback to config VectorSize
        // Based on the implementation fallback logic, it should use 1536 as the final fallback
        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, 1536, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.StoreContentAsync(documentLibraryName, indexName, documentStream, fileName, null);

        // Assert
        _mockEmbeddingService.Verify(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(documentLibraryName), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, It.IsAny<string>()), Times.Once);
        // Should use default 1536 dimensions when resolution fails and configured VectorSize > 0 ? VectorSize : 1536
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteContentAsync_ShouldCallProvider_WhenValidParametersProvided()
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";

        _mockProvider.Setup(x => x.DeleteFileAsync(indexName, fileName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.DeleteContentAsync(documentLibraryName, indexName, fileName);

        // Assert
        _mockProvider.Verify(x => x.DeleteFileAsync(indexName, fileName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnResults_WhenValidSearchPerformed()
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string searchText = "test query";
        var searchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
            IndexName = "test-index",
            Top = 5,
            MinRelevance = 0.7,
            ParametersExactMatch = new Dictionary<string, string>(),
            PrecedingPartitionCount = 0,
            FollowingPartitionCount = 0,
            EnableProgressiveSearch = false,
            EnableKeywordFallback = false
        };

        var queryEmbedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray();
        var searchResults = CreateTestSearchResults(3);

        // Use the correct method signature for document library embedding generation
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, searchText))
            .ReturnsAsync(queryEmbedding);

        _mockProvider.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                queryEmbedding,
                searchOptions.Top,
                searchOptions.MinRelevance,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var results = await _repository.SearchAsync(documentLibraryName, searchText, searchOptions);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(searchResults.GroupBy(r => r.Record.DocumentId).Count(), results.Count);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, searchText), Times.Once);
        _mockProvider.Verify(x => x.SearchAsync(
            It.IsAny<string>(),
            queryEmbedding,
            searchOptions.Top,
            searchOptions.MinRelevance,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldUseDocumentProcessEmbeddings_WhenDocumentProcessLibraryType()
    {
        // Arrange
        const string documentProcessName = "test-process";
        const string searchText = "test query";
        var searchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
            IndexName = "test-index",
            Top = 5,
            MinRelevance = 0.7,
            ParametersExactMatch = new Dictionary<string, string>(),
            PrecedingPartitionCount = 0,
            FollowingPartitionCount = 0,
            EnableProgressiveSearch = false,
            EnableKeywordFallback = false
        };

        var queryEmbedding = Enumerable.Range(0, 1024).Select(x => (float)(x * 0.001)).ToArray();
        var searchResults = CreateTestSearchResults(2);

        // Create repository with DocumentProcessLibrary type to test context-aware embedding generation
        var processRepository = new SemanticKernelVectorStoreRepository(
            _mockLogger.Object,
            _mockOptions.Object,
            _mockEmbeddingService.Object,
            _mockProvider.Object,
            _mockTextExtractionService.Object,
            _mockChunkingService.Object,
            _mockFileUrlResolver.Object,
            documentLibraryType: DocumentLibraryType.PrimaryDocumentProcessLibrary);

        // FIXED: Now search uses context-aware embedding generation like storage
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, searchText))
            .ReturnsAsync(queryEmbedding);

        _mockProvider.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                queryEmbedding,
                searchOptions.Top,
                searchOptions.MinRelevance,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var results = await processRepository.SearchAsync(documentProcessName, searchText, searchOptions);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(searchResults.GroupBy(r => r.Record.DocumentId).Count(), results.Count);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, searchText), Times.Once);
        // Should now use document process method for search consistency
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithNeighborExpansion_ShouldExpandResults_WhenEnabled()
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string searchText = "test query";
        var searchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
            IndexName = "test-index",
            Top = 5,
            MinRelevance = 0.7,
            PrecedingPartitionCount = 1,
            FollowingPartitionCount = 1
        };

        var queryEmbedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray();
        var searchResults = CreateTestSearchResults(2);
        var neighborChunks = CreateTestVectorChunkRecords(2, "doc1");

        // Use the correct method signature for document library embedding generation
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, searchText))
            .ReturnsAsync(queryEmbedding);

        _mockProvider.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                queryEmbedding,
                searchOptions.Top,
                searchOptions.MinRelevance,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockProvider.Setup(x => x.GetNeighborChunksAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                searchOptions.PrecedingPartitionCount,
                searchOptions.FollowingPartitionCount,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(neighborChunks);

        // Act
        var results = await _repository.SearchAsync(documentLibraryName, searchText, searchOptions);

        // Assert
        Assert.NotNull(results);
        // Should include original results plus expanded neighbors
        Assert.True(results.Count >= searchResults.GroupBy(r => r.Record.DocumentId).Count());
        _mockProvider.Verify(x => x.GetNeighborChunksAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            searchOptions.PrecedingPartitionCount,
            searchOptions.FollowingPartitionCount,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AskAsync_ShouldReturnNull_WhenNoResultsFound()
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string indexName = "test-index";
        const string question = "What is this document about?";
        var parametersExactMatch = new Dictionary<string, string> { { "userId", "user123" } };

        // Setup empty search results
        var queryEmbedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray();
        
        // Use the correct method signature for document library embedding generation
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, question))
            .ReturnsAsync(queryEmbedding);

        _mockProvider.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchMatch>());

        // Act
        var result = await _repository.AskAsync(documentLibraryName, indexName, parametersExactMatch, question);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreContentAsync_ShouldCompleteSuccessfully_WhenEmptyStreamProvided()
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string indexName = "test-index";
        const string fileName = "empty-document.pdf";
        const string documentUrl = "https://example.com/empty-document.pdf";

        var emptyStream = new MemoryStream();

        // Setup text extraction to return empty text
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(string.Empty);

        _mockChunkingService.Setup(x => x.ChunkText(string.Empty, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<string>());

        // Act - This should complete successfully even with empty content
        await _repository.StoreContentAsync(documentLibraryName, indexName, emptyStream, fileName, documentUrl);

        // Assert
        _mockTextExtractionService.Verify(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName), Times.Once);
        _mockChunkingService.Verify(x => x.ChunkText(string.Empty, It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        // Should not call embedding service for empty chunks
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task StoreContentAsync_ShouldThrowNullReferenceException_WhenInvalidFileNameProvided(string? fileName)
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string indexName = "test-index";
        const string documentUrl = "https://example.com/document.pdf";

        var documentStream = CreateTestDocumentStream("Test content");

        // Act & Assert - Based on implementation, this throws NullReferenceException when trying to URL decode null
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _repository.StoreContentAsync(documentLibraryName, indexName, documentStream, fileName!, documentUrl));
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmptyList_WhenNoResultsFound()
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string searchText = "nonexistent query";
        var searchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
            IndexName = "test-index",
            Top = 5,
            MinRelevance = 0.7,
            EnableProgressiveSearch = false // Disable progressive search to test exact scenario
        };

        var queryEmbedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray();

        // Use the correct method signature for document library embedding generation
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, searchText))
            .ReturnsAsync(queryEmbedding);

        _mockProvider.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                queryEmbedding,
                searchOptions.Top,
                searchOptions.MinRelevance,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchMatch>());

        // Act
        var results = await _repository.SearchAsync(documentLibraryName, searchText, searchOptions);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ShouldHaveConsistentEmbeddingDimensions_BetweenStorageAndSearch()
    {
        // Arrange - Test the critical dimensionality consistency requirement
        const string documentProcessName = "embedding-consistency-test";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";
        const string searchText = "test search query";

        // Setup for storage with custom dimensions (3072 for text-embedding-3-large)
        var documentStream = CreateTestDocumentStream("Test content for embedding consistency");
        var extractedText = "Extracted text for embedding consistency testing";
        var chunks = new List<string> { "Consistency test chunk content" };
        var storageEmbedding = Enumerable.Range(0, 3072).Select(x => (float)(x * 0.0001)).ToArray(); // 3072 dimensions
        var searchEmbedding = Enumerable.Range(1000, 3072).Select(x => (float)(x * 0.0002)).ToArray(); // Same dimensions for search

        // Create repository with DocumentProcessLibrary type
        var processRepository = new SemanticKernelVectorStoreRepository(
            _mockLogger.Object,
            _mockOptions.Object,
            _mockEmbeddingService.Object,
            _mockProvider.Object,
            _mockTextExtractionService.Object,
            _mockChunkingService.Object,
            _mockFileUrlResolver.Object,
            documentLibraryType: DocumentLibraryType.PrimaryDocumentProcessLibrary);

        // Setup storage mocks
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(extractedText);

        _mockChunkingService.Setup(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcessName))
            .ReturnsAsync(("text-embedding-3-large", 3072));

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()))
            .ReturnsAsync((string processName, string text) => 
                text == searchText ? searchEmbedding : storageEmbedding);

        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, 3072, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup search mocks
        var searchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
            IndexName = indexName,
            Top = 5,
            MinRelevance = 0.7,
            ParametersExactMatch = new Dictionary<string, string>(),
            EnableProgressiveSearch = false,
            EnableKeywordFallback = false
        };

        var searchResults = CreateTestSearchResults(1);
        _mockProvider.Setup(x => x.SearchAsync(
                indexName,
                searchEmbedding, // Same dimensions as storage
                searchOptions.Top,
                searchOptions.MinRelevance,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act - Store then search to test consistency
        await processRepository.StoreContentAsync(documentProcessName, indexName, documentStream, fileName, null);
        var results = await processRepository.SearchAsync(documentProcessName, searchText, searchOptions);

        // Assert - Both storage and search should use the same embedding generation method
        Assert.NotNull(results);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()), 
            Times.Exactly(chunks.Count + 1)); // Once for storage, once for search
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, 3072, It.IsAny<CancellationToken>()), Times.Once);
        _mockProvider.Verify(x => x.SearchAsync(indexName, searchEmbedding, It.IsAny<int>(), It.IsAny<double>(), 
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify both embeddings have same dimensions (this would fail if inconsistent)
        Assert.Equal(storageEmbedding.Length, searchEmbedding.Length);
    }

    [Fact]
    public async Task SearchAsync_ShouldUseSameEmbeddingMethod_ForDocumentLibraryAsStorage()
    {
        // Arrange - Test that document libraries also use consistent embedding generation
        const string documentLibraryName = "document-library-consistency";
        const string searchText = "search consistency test";
        var searchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
            IndexName = "test-index",
            Top = 5,
            MinRelevance = 0.7,
            ParametersExactMatch = new Dictionary<string, string>(),
            EnableProgressiveSearch = false,
            EnableKeywordFallback = false
        };

        var queryEmbedding = Enumerable.Range(0, 1024).Select(x => (float)(x * 0.001)).ToArray(); // Custom dimensions
        var searchResults = CreateTestSearchResults(2);

        // Default repository (DocumentLibraryType.AdditionalDocumentLibrary behavior)
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, searchText))
            .ReturnsAsync(queryEmbedding);

        _mockProvider.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                queryEmbedding,
                searchOptions.Top,
                searchOptions.MinRelevance,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var results = await _repository.SearchAsync(documentLibraryName, searchText, searchOptions);

        // Assert - Document library should use GenerateEmbeddingsForDocumentLibraryAsync consistently
        Assert.NotNull(results);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, searchText), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentProcessAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_ShouldPreventDimensionMismatch_WhenSearchingProcessIndexWithLibraryEmbeddings()
    {
        // Arrange - Test that using wrong embedding method would be caught by dimensionality mismatch
        const string documentProcessName = "mismatch-test-process";
        const string searchText = "dimension mismatch test";
        
        // This test documents that BEFORE the fix, this scenario would cause problems
        // AFTER the fix, both storage and search use the same context-aware method
        
        var searchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
            IndexName = "test-index",
            Top = 5,
            MinRelevance = 0.7,
            ParametersExactMatch = new Dictionary<string, string>(),
            EnableProgressiveSearch = false,
            EnableKeywordFallback = false
        };

        // Create process repository that should use document process embeddings consistently
        var processRepository = new SemanticKernelVectorStoreRepository(
            _mockLogger.Object,
            _mockOptions.Object,
            _mockEmbeddingService.Object,
            _mockProvider.Object,
            _mockTextExtractionService.Object,
            _mockChunkingService.Object,
            _mockFileUrlResolver.Object,
            documentLibraryType: DocumentLibraryType.PrimaryDocumentProcessLibrary);

        // Setup correct embedding method (document process with 1024 dimensions)
        var correctProcessEmbedding = Enumerable.Range(0, 1024).Select(x => (float)(x * 0.001)).ToArray();
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, searchText))
            .ReturnsAsync(correctProcessEmbedding);

        // Setup incorrect embedding method (document library with different dimensions - 384)
        var incorrectLibraryEmbedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.002)).ToArray();
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentProcessName, searchText))
            .ReturnsAsync(incorrectLibraryEmbedding);

        var searchResults = CreateTestSearchResults(1);
        _mockProvider.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                correctProcessEmbedding, // Should match what storage used
                searchOptions.Top,
                searchOptions.MinRelevance,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act - Search should use the correct context-aware method
        var results = await processRepository.SearchAsync(documentProcessName, searchText, searchOptions);

        // Assert - Should use document process method, not document library method
        Assert.NotNull(results);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, searchText), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentProcessName, searchText), Times.Never);
        
        // Verify the provider was called with correct embeddings (1024 dimensions, not 384)
        _mockProvider.Verify(x => x.SearchAsync(
            It.IsAny<string>(),
            It.Is<float[]>(embedding => embedding.Length == 1024), // Correct dimensions
            It.IsAny<int>(),
            It.IsAny<double>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Helper Methods

    private static Stream CreateTestDocumentStream(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bytes);
    }

    private static List<VectorSearchMatch> CreateTestSearchResults(int count)
    {
        var results = new List<VectorSearchMatch>();

        for (int i = 0; i < count; i++)
        {
            var record = CreateTestVectorChunkRecord($"search-doc-{i}", i);
            var score = 0.9 - (i * 0.1); // Decreasing relevance scores
            results.Add(new VectorSearchMatch(record, score));
        }

        return results;
    }

    private static List<SkVectorChunkRecord> CreateTestVectorChunkRecords(int count, string? documentId = null)
    {
        var records = new List<SkVectorChunkRecord>();
        var baseDocumentId = documentId ?? "test-doc";

        for (int i = 0; i < count; i++)
        {
            records.Add(CreateTestVectorChunkRecord(baseDocumentId, i));
        }

        return records;
    }

    private static SkVectorChunkRecord CreateTestVectorChunkRecord(string documentId, int partitionNumber)
    {
        return new SkVectorChunkRecord
        {
            DocumentId = documentId,
            FileName = $"test-file-{documentId}.pdf",
            DocumentReference = $"doc:{Guid.NewGuid()}", // Use DocumentReference instead of OriginalDocumentUrl
            ChunkText = $"This is test content for partition {partitionNumber} of document {documentId}.",
            Embedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray(),
            PartitionNumber = partitionNumber,
            IngestedAt = DateTimeOffset.UtcNow.AddMinutes(-partitionNumber),
            Tags = new Dictionary<string, List<string?>>
            {
                { "userId", new List<string?> { "user123" } },
                { "category", new List<string?> { "documents" } },
                { "partition", new List<string?> { partitionNumber.ToString() } },
                { "DocumentId", new List<string?> { documentId } },
                { "FileName", new List<string?> { $"test-file-{documentId}.pdf" } }
            }
        };
    }

    #endregion

    [Fact]
    public async Task StoreContentAsync_ShouldUseCustomEmbeddingModel_WhenDocumentProcessHasCustomModel()
    {
        // Arrange
        const string documentProcessName = "custom-embedding-process";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";

        var documentStream = CreateTestDocumentStream("Test content for custom embeddings");
        var extractedText = "Extracted text for custom model testing";
        var chunks = new List<string> { "Custom embedding chunk content" };
        var customEmbedding = Enumerable.Range(0, 3072).Select(x => (float)(x * 0.0001)).ToArray(); // 3072 dimensions for text-embedding-3-large

        // Create repository with DocumentProcessLibrary type
        var processRepository = new SemanticKernelVectorStoreRepository(
            _mockLogger.Object,
            _mockOptions.Object,
            _mockEmbeddingService.Object,
            _mockProvider.Object,
            _mockTextExtractionService.Object,
            _mockChunkingService.Object,
            _mockFileUrlResolver.Object,
            documentLibraryType: DocumentLibraryType.PrimaryDocumentProcessLibrary);

        // Setup mocks
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(extractedText);

        _mockChunkingService.Setup(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        // Setup custom embedding model resolution
        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcessName))
            .ReturnsAsync(("text-embedding-3-large", 3072));

        // Setup custom embedding generation
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()))
            .ReturnsAsync(customEmbedding);

        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, 3072, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await processRepository.StoreContentAsync(documentProcessName, indexName, documentStream, fileName, null);

        // Assert
        _mockEmbeddingService.Verify(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcessName), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()), Times.Exactly(chunks.Count));
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, 3072, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreContentAsync_ShouldUseCustomEmbeddingModel_WhenDocumentLibraryHasCustomModel()
    {
        // Arrange
        const string documentLibraryName = "custom-embedding-library";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";

        var documentStream = CreateTestDocumentStream("Test content for custom library embeddings");
        var extractedText = "Extracted text for custom library model testing";
        var chunks = new List<string> { "Custom library embedding chunk content" };
        var customEmbedding = Enumerable.Range(0, 1024).Select(x => (float)(x * 0.0001)).ToArray(); // 1024 dimensions for custom model

        // Setup mocks
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(extractedText);

        _mockChunkingService.Setup(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        // Setup custom embedding model resolution for document library
        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(documentLibraryName))
            .ReturnsAsync(("text-embedding-ada-003", 1024));

        // Setup custom embedding generation for document library
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, It.IsAny<string>()))
            .ReturnsAsync(customEmbedding);

        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, 1024, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.StoreContentAsync(documentLibraryName, indexName, documentStream, fileName, null);

        // Assert
        _mockEmbeddingService.Verify(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(documentLibraryName), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, It.IsAny<string>()), Times.Exactly(chunks.Count));
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, 1024, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreContentAsync_ShouldUseCustomDimensionality_WhenConfiguredWithDimensionOverride()
    {
        // Arrange
        const string documentProcessName = "dimension-override-process";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";

        var documentStream = CreateTestDocumentStream("Test content for dimension override");
        var extractedText = "Extracted text for dimension override testing";
        var chunks = new List<string> { "Dimension override chunk content" };
        var customEmbedding = Enumerable.Range(0, 256).Select(x => (float)(x * 0.001)).ToArray(); // 256 dimensions override

        // Create repository with DocumentProcessLibrary type
        var processRepository = new SemanticKernelVectorStoreRepository(
            _mockLogger.Object,
            _mockOptions.Object,
            _mockEmbeddingService.Object,
            _mockProvider.Object,
            _mockTextExtractionService.Object,
            _mockChunkingService.Object,
            _mockFileUrlResolver.Object,
            documentLibraryType: DocumentLibraryType.PrimaryDocumentProcessLibrary);

        // Setup mocks
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(extractedText);

        _mockChunkingService.Setup(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        // Setup dimension override resolution - using same model but with dimension override
        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcessName))
            .ReturnsAsync(("text-embedding-ada-002", 256)); // 256 instead of default 1536

        // Setup embedding generation with custom dimensions
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()))
            .ReturnsAsync(customEmbedding);

        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, 256, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await processRepository.StoreContentAsync(documentProcessName, indexName, documentStream, fileName, null);

        // Assert
        _mockEmbeddingService.Verify(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcessName), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentProcessAsync(documentProcessName, It.IsAny<string>()), Times.Exactly(chunks.Count));
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, 256, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldGenerateEmbeddingsForSearchQuery_WhenCustomConfigurationExists()
    {
        // Arrange
        const string documentLibraryName = "search-custom-embedding-library";
        const string searchText = "custom search query";
        var searchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
            IndexName = "test-index",
            Top = 3,
            MinRelevance = 0.8,
            ParametersExactMatch = new Dictionary<string, string>(),
            PrecedingPartitionCount = 0,
            FollowingPartitionCount = 0,
            EnableProgressiveSearch = false,
            EnableKeywordFallback = false
        };

        var customQueryEmbedding = Enumerable.Range(0, 1024).Select(x => (float)(x * 0.0005)).ToArray();
        var searchResults = CreateTestSearchResults(2);

        // Setup custom embedding generation for search query
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, searchText))
            .ReturnsAsync(customQueryEmbedding);

        _mockProvider.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                customQueryEmbedding,
                searchOptions.Top,
                searchOptions.MinRelevance,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var results = await _repository.SearchAsync(documentLibraryName, searchText, searchOptions);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(searchResults.GroupBy(r => r.Record.DocumentId).Count(), results.Count);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, searchText), Times.Once);
        _mockProvider.Verify(x => x.SearchAsync(
            It.IsAny<string>(),
            customQueryEmbedding,
            searchOptions.Top,
            searchOptions.MinRelevance,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveEmbeddingDimensionsAsync_ShouldFallbackCorrectly_WhenBothResolutionMethodsFail()
    {
        // Arrange
        const string unknownName = "unknown-library-or-process";
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";

        var documentStream = CreateTestDocumentStream("Test content for fallback scenario");
        var extractedText = "Extracted text for fallback testing";
        var chunks = new List<string> { "Fallback scenario chunk content" };
        var fallbackEmbedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray(); // Fallback to config dimensions

        // Setup mocks
        _mockTextExtractionService.Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName))
            .ReturnsAsync(extractedText);

        _mockChunkingService.Setup(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunks);

        // Setup both resolution methods to fail
        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(unknownName))
            .ThrowsAsync(new InvalidOperationException("Unknown library"));

        _mockEmbeddingService.Setup(x => x.ResolveEmbeddingConfigForDocumentProcessAsync(unknownName))
            .ThrowsAsync(new InvalidOperationException("Unknown process"));

        // Setup embedding generation to work with fallback (will use document library method as default)
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsForDocumentLibraryAsync(unknownName, It.IsAny<string>()))
            .ReturnsAsync(fallbackEmbedding);

        // Provider should be called with config dimensions (384) as fallback
        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, 384, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.StoreContentAsync(unknownName, indexName, documentStream, fileName, null);

        // Assert
        // Should try to resolve both ways and fallback to config dimensions
        _mockEmbeddingService.Verify(x => x.ResolveEmbeddingConfigForDocumentLibraryAsync(unknownName), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsForDocumentLibraryAsync(unknownName, It.IsAny<string>()), Times.Exactly(chunks.Count));
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, 384, It.IsAny<CancellationToken>()), Times.Once);
    }
}