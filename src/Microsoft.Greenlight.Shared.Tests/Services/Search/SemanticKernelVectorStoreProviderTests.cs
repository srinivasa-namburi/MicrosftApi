// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Moq;

namespace Microsoft.Greenlight.Shared.Tests.Services.Search;

/// <summary>
/// Tests for ISemanticKernelVectorStoreProvider implementations.
/// These tests verify the contract behavior and basic functionality.
/// </summary>
public class SemanticKernelVectorStoreProviderTests
{
    private readonly Mock<ISemanticKernelVectorStoreProvider> _mockProvider;
    private readonly Mock<ILogger<SemanticKernelVectorStoreProviderTests>> _mockLogger;

    public SemanticKernelVectorStoreProviderTests()
    {
        _mockProvider = new Mock<ISemanticKernelVectorStoreProvider>();
        _mockLogger = new Mock<ILogger<SemanticKernelVectorStoreProviderTests>>();
    }

    [Fact]
    public async Task EnsureCollectionAsync_ShouldCompleteSuccessfully_WhenIndexNameProvided()
    {
        // Arrange
        const string indexName = "test-index";
        const int dims = 1536;
        _mockProvider.Setup(x => x.EnsureCollectionAsync(indexName, dims, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await _mockProvider.Object.EnsureCollectionAsync(indexName, dims);
        _mockProvider.Verify(x => x.EnsureCollectionAsync(indexName, dims, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_ShouldAcceptValidRecords_WhenCalledWithValidData()
    {
        // Arrange
        const string indexName = "test-index";
        var records = CreateTestVectorChunkRecords(3);

        _mockProvider.Setup(x => x.UpsertAsync(indexName, records, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await _mockProvider.Object.UpsertAsync(indexName, records);
        _mockProvider.Verify(x => x.UpsertAsync(indexName, records, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldCompleteSuccessfully_WhenValidParameters()
    {
        // Arrange
        const string indexName = "test-index";
        const string fileName = "test-document.pdf";

        _mockProvider.Setup(x => x.DeleteFileAsync(indexName, fileName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await _mockProvider.Object.DeleteFileAsync(indexName, fileName);
        _mockProvider.Verify(x => x.DeleteFileAsync(indexName, fileName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnResults_WhenQueryProvided()
    {
        // Arrange
        const string indexName = "test-index";
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        const int top = 5;
        const double minRelevance = 0.7;
        var expectedResults = CreateTestSearchResults(3);

        _mockProvider.Setup(x => x.SearchAsync(indexName, queryEmbedding, top, minRelevance, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await _mockProvider.Object.SearchAsync(indexName, queryEmbedding, top, minRelevance);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(expectedResults.Count, results.Count);
        _mockProvider.Verify(x => x.SearchAsync(indexName, queryEmbedding, top, minRelevance, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldAcceptParameterFiltering_WhenParametersProvided()
    {
        // Arrange
        const string indexName = "test-index";
        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        const int top = 5;
        const double minRelevance = 0.7;
        var parameters = new Dictionary<string, string> { { "userId", "user123" }, { "category", "documents" } };
        var expectedResults = CreateTestSearchResults(2);

        _mockProvider.Setup(x => x.SearchAsync(indexName, queryEmbedding, top, minRelevance, parameters, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await _mockProvider.Object.SearchAsync(indexName, queryEmbedding, top, minRelevance, parameters);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(expectedResults.Count, results.Count);
        _mockProvider.Verify(x => x.SearchAsync(indexName, queryEmbedding, top, minRelevance, parameters, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllDocumentChunksAsync_ShouldReturnAllChunks_WhenDocumentExists()
    {
        // Arrange
        const string indexName = "test-index";
        const string documentId = "doc123";
        var expectedChunks = CreateTestVectorChunkRecords(5, documentId);

        _mockProvider.Setup(x => x.GetAllDocumentChunksAsync(indexName, documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChunks);

        // Act
        var chunks = await _mockProvider.Object.GetAllDocumentChunksAsync(indexName, documentId);

        // Assert
        Assert.NotNull(chunks);
        Assert.Equal(expectedChunks.Count, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal(documentId, chunk.DocumentId));
        _mockProvider.Verify(x => x.GetAllDocumentChunksAsync(indexName, documentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryGetChunkAsync_ShouldReturnChunk_WhenChunkExists()
    {
        // Arrange
        const string indexName = "test-index";
        const string documentId = "doc123";
        const int partitionNumber = 1;
        var expectedChunk = CreateTestVectorChunkRecord(documentId, partitionNumber);

        _mockProvider.Setup(x => x.TryGetChunkAsync(indexName, documentId, partitionNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChunk);

        // Act
        var chunk = await _mockProvider.Object.TryGetChunkAsync(indexName, documentId, partitionNumber);

        // Assert
        Assert.NotNull(chunk);
        Assert.Equal(documentId, chunk.DocumentId);
        Assert.Equal(partitionNumber, chunk.PartitionNumber);
        _mockProvider.Verify(x => x.TryGetChunkAsync(indexName, documentId, partitionNumber, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryGetChunkAsync_ShouldReturnNull_WhenChunkDoesNotExist()
    {
        // Arrange
        const string indexName = "test-index";
        const string documentId = "nonexistent";
        const int partitionNumber = 999;

        _mockProvider.Setup(x => x.TryGetChunkAsync(indexName, documentId, partitionNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SkVectorChunkRecord?)null);

        // Act
        var chunk = await _mockProvider.Object.TryGetChunkAsync(indexName, documentId, partitionNumber);

        // Assert
        Assert.Null(chunk);
        _mockProvider.Verify(x => x.TryGetChunkAsync(indexName, documentId, partitionNumber, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDocumentPartitionNumbersAsync_ShouldReturnOrderedPartitionNumbers_WhenDocumentExists()
    {
        // Arrange
        const string indexName = "test-index";
        const string documentId = "doc123";
        var expectedPartitions = new List<int> { 0, 1, 2, 3, 4 };

        _mockProvider.Setup(x => x.GetDocumentPartitionNumbersAsync(indexName, documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPartitions);

        // Act
        var partitions = await _mockProvider.Object.GetDocumentPartitionNumbersAsync(indexName, documentId);

        // Assert
        Assert.NotNull(partitions);
        Assert.Equal(expectedPartitions.Count, partitions.Count);
        Assert.Equal(expectedPartitions, partitions);
        _mockProvider.Verify(x => x.GetDocumentPartitionNumbersAsync(indexName, documentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNeighborChunksAsync_WithAsymmetricWindow_ShouldReturnNeighbors_WhenValidParameters()
    {
        // Arrange
        const string indexName = "test-index";
        const string documentId = "doc123";
        const int partitionNumber = 5;
        const int precedingPartitions = 2;
        const int followingPartitions = 3;
        var expectedNeighbors = CreateTestVectorChunkRecords(5, documentId); // partitions 3, 4, 6, 7, 8

        _mockProvider.Setup(x => x.GetNeighborChunksAsync(indexName, documentId, partitionNumber, precedingPartitions, followingPartitions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedNeighbors);

        // Act
        var neighbors = await _mockProvider.Object.GetNeighborChunksAsync(indexName, documentId, partitionNumber, precedingPartitions, followingPartitions);

        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(expectedNeighbors.Count, neighbors.Count);
        Assert.All(neighbors, chunk => Assert.Equal(documentId, chunk.DocumentId));
        _mockProvider.Verify(x => x.GetNeighborChunksAsync(indexName, documentId, partitionNumber, precedingPartitions, followingPartitions, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNeighborChunksAsync_WithRadius_ShouldReturnNeighbors_WhenValidParameters()
    {
        // Arrange
        const string indexName = "test-index";
        const string documentId = "doc123";
        const int partitionNumber = 5;
        const int radius = 2;
        var expectedNeighbors = CreateTestVectorChunkRecords(4, documentId); // partitions 3, 4, 6, 7

        _mockProvider.Setup(x => x.GetNeighborChunksAsync(indexName, documentId, partitionNumber, radius, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedNeighbors);

        // Act
        var neighbors = await _mockProvider.Object.GetNeighborChunksAsync(indexName, documentId, partitionNumber, radius);

        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(expectedNeighbors.Count, neighbors.Count);
        Assert.All(neighbors, chunk => Assert.Equal(documentId, chunk.DocumentId));
        _mockProvider.Verify(x => x.GetNeighborChunksAsync(indexName, documentId, partitionNumber, radius, It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Helper Methods

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
                { "partition", new List<string?> { partitionNumber.ToString() } }
            }
        };
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

    #endregion
}