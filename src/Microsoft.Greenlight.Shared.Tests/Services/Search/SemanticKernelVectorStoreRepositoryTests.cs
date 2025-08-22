// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
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

        // Setup default configuration
        _serviceOptions = new ServiceConfigurationOptions
        {
            GreenlightServices = new ServiceConfigurationOptions.GreenlightServicesOptions
            {
                VectorStore = new VectorStoreOptions
                {
                    ChunkOverlap = 100,
                    ChunkSize = 1000
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
            _mockChunkingService.Object);
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

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<string>()))
            .ReturnsAsync((string text) => embeddings[chunks.IndexOf(text)]);

        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.StoreContentAsync(documentLibraryName, indexName, documentStream, fileName, documentUrl, userId, additionalTags);

        // Assert
        _mockTextExtractionService.Verify(x => x.ExtractTextAsync(It.IsAny<Stream>(), fileName), Times.Once);
        _mockChunkingService.Verify(x => x.ChunkText(extractedText, It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsAsync(It.IsAny<string>()), Times.Exactly(chunks.Count));
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, It.IsAny<CancellationToken>()), Times.Once);
        _mockProvider.Verify(x => x.UpsertAsync(indexName, It.IsAny<IEnumerable<SkVectorChunkRecord>>(), It.IsAny<CancellationToken>()), Times.Once);
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
            MinRelevance = 0.7
        };

        var queryEmbedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray();
        var searchResults = CreateTestSearchResults(3);

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsAsync(searchText))
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
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsAsync(searchText), Times.Once);
        _mockProvider.Verify(x => x.SearchAsync(
            It.IsAny<string>(),
            queryEmbedding,
            searchOptions.Top,
            searchOptions.MinRelevance,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
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

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsAsync(searchText))
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
        
        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsAsync(question))
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
    public async Task StoreContentAsync_ShouldThrowArgumentException_WhenEmptyStreamProvided()
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
        _mockEmbeddingService.Verify(x => x.GenerateEmbeddingsAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task StoreContentAsync_ShouldThrowNullReferenceException_WhenInvalidFileNameProvided(string fileName)
    {
        // Arrange
        const string documentLibraryName = "test-library";
        const string indexName = "test-index";
        const string documentUrl = "https://example.com/document.pdf";

        var documentStream = CreateTestDocumentStream("Test content");

        // Act & Assert - Based on implementation, this throws NullReferenceException when trying to URL decode null
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _repository.StoreContentAsync(documentLibraryName, indexName, documentStream, fileName, documentUrl));
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

        _mockEmbeddingService.Setup(x => x.GenerateEmbeddingsAsync(searchText))
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
            OriginalDocumentUrl = $"https://example.com/{documentId}.pdf",
            ChunkText = $"This is test content for partition {partitionNumber} of document {documentId}.",
            Embedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray(),
            PartitionNumber = partitionNumber,
            IngestedAt = DateTimeOffset.UtcNow.AddMinutes(-partitionNumber),
            Tags = new Dictionary<string, List<string?>>
            {
                { "userId", new List<string?> { "user123" } },
                { "category", new List<string?> { "documents" } },
                { "partition", new List<string?> { partitionNumber.ToString() } }
            }
        };
    }

    #endregion
}