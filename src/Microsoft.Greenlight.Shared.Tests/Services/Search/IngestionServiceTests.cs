// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Moq;
using System.Text;

namespace Microsoft.Greenlight.Shared.Tests.Services.Search;

/// <summary>
/// Tests for the BasicTextExtractionService (current simplified API).
/// </summary>
public class BasicTextExtractionServiceTests
{
    private readonly Mock<ILogger<BasicTextExtractionService>> _mockLogger;
    private readonly BasicTextExtractionService _service;

    public BasicTextExtractionServiceTests()
    {
        _mockLogger = new Mock<ILogger<BasicTextExtractionService>>();
        _service = new BasicTextExtractionService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExtractTextAsync_ShouldReturnText_ForPlainText()
    {
        var testText = "This is a test document with multiple lines.\nSecond line here.";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(testText));

        var result = await _service.ExtractTextAsync(stream, "test.txt");

        Assert.Equal(testText, result);
    }

    [Fact]
    public async Task ExtractTextAsync_ShouldReturnEmpty_ForUnsupportedType()
    {
        var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // PDF header
        var result = await _service.ExtractTextAsync(stream, "test.pdf");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ExtractTextAsync_ShouldHandleEmptyStream()
    {
        var stream = new MemoryStream();
        var result = await _service.ExtractTextAsync(stream, "empty.txt");
        Assert.Equal(string.Empty, result);
    }
}

// BasicTextChunkingService removed from codebase; tests deleted accordingly.

/// <summary>
/// Tests for the BatchDocumentIngestionService.
/// </summary>
public class BatchDocumentIngestionServiceTests
{
    private readonly Mock<IDocumentIngestionService> _mockIngestionService;
    private readonly Mock<ILogger<BatchDocumentIngestionService>> _mockLogger;
    private readonly BatchDocumentIngestionService _service;

    public BatchDocumentIngestionServiceTests()
    {
        _mockIngestionService = new Mock<IDocumentIngestionService>();
        _mockLogger = new Mock<ILogger<BatchDocumentIngestionService>>();
        _service = new BatchDocumentIngestionService(_mockIngestionService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task IngestDocumentsBatchAsync_ShouldProcessAllDocuments_WhenAllSucceed()
    {
        // Arrange
        var documents = new List<BatchDocumentRequest>
        {
            CreateBatchRequest("doc1.txt"),
            CreateBatchRequest("doc2.txt"),
            CreateBatchRequest("doc3.txt")
        };

        _mockIngestionService
            .Setup(x => x.IngestDocumentAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync(DocumentIngestionResult.Ok(5, 1000));

        // Act
        var result = await _service.IngestDocumentsBatchAsync(documents);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(15, result.TotalChunkCount); // 3 docs * 5 chunks each
        Assert.Equal(3000, result.TotalDocumentSizeBytes); // 3 docs * 1000 bytes each
        Assert.Equal(3, result.IndividualResults.Count);
    }

    [Fact]
    public async Task IngestDocumentsBatchAsync_ShouldHandleMixedResults()
    {
        // Arrange
        var documents = new List<BatchDocumentRequest>
        {
            CreateBatchRequest("doc1.txt"),
            CreateBatchRequest("doc2.txt"),
            CreateBatchRequest("doc3.txt")
        };

        _mockIngestionService
            .SetupSequence(x => x.IngestDocumentAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync(DocumentIngestionResult.Ok(5, 1000))
            .ReturnsAsync(DocumentIngestionResult.Fail("Extraction failed"))
            .ReturnsAsync(DocumentIngestionResult.Ok(3, 800));

        // Act
        var result = await _service.IngestDocumentsBatchAsync(documents);

        // Assert
        Assert.False(result.Success); // Overall failure due to one failed document
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(8, result.TotalChunkCount); // 5 + 3 chunks from successful docs
        Assert.Equal(1800, result.TotalDocumentSizeBytes); // 1000 + 800 bytes from successful docs
        Assert.Equal(3, result.IndividualResults.Count);
    }

    [Fact]
    public async Task DeleteDocumentsBatchAsync_ShouldProcessAllDeletions()
    {
        // Arrange
        var deleteRequests = new List<BatchDocumentDeleteRequest>
        {
            new() { DocumentLibraryName = "lib1", IndexName = "index1", FileName = "doc1.txt" },
            new() { DocumentLibraryName = "lib1", IndexName = "index1", FileName = "doc2.txt" }
        };

        _mockIngestionService
            .Setup(x => x.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(DocumentIngestionResult.Ok(0, 0));

        // Act
        var result = await _service.DeleteDocumentsBatchAsync(deleteRequests);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(0, result.TotalChunkCount); // No chunks for deletion
        Assert.Equal(0, result.TotalDocumentSizeBytes); // No size for deletion
        Assert.Equal(2, result.IndividualResults.Count);
    }

    [Fact]
    public async Task IngestDocumentsBatchAsync_ShouldHandleExceptions()
    {
        // Arrange
        var documents = new List<BatchDocumentRequest>
        {
            CreateBatchRequest("doc1.txt")
        };

        _mockIngestionService
            .Setup(x => x.IngestDocumentAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        var result = await _service.IngestDocumentsBatchAsync(documents);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Single(result.IndividualResults);
        Assert.Contains("Batch processing error", result.IndividualResults[0].ErrorMessage);
    }

    private static BatchDocumentRequest CreateBatchRequest(string fileName)
    {
        return new BatchDocumentRequest
        {
            DocumentId = Guid.NewGuid(),
            FileStream = new MemoryStream(Encoding.UTF8.GetBytes("Test content")),
            FileName = fileName,
            DocumentUrl = $"https://example.com/{fileName}",
            DocumentLibraryName = "TestLibrary",
            IndexName = "TestIndex",
            UserId = "testuser"
        };
    }
}
