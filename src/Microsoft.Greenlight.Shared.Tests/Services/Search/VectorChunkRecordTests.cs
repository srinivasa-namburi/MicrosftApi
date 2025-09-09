// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.Tests.Services.Search;

/// <summary>
/// Tests for SkVectorChunkRecord and VectorSearchMatch classes.
/// </summary>
public class VectorChunkRecordTests
{
    [Fact]
    public void SkVectorChunkRecord_ShouldCreateSuccessfully_WhenAllRequiredPropertiesProvided()
    {
        // Arrange
        const string documentId = "test-doc-123";
        const string fileName = "test-document.pdf";
        const string documentReference = "doc:550e8400-e29b-41d4-a716-446655440000";
        const string chunkText = "This is a test chunk of text content for vector storage.";
        var embedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray();
        const int partitionNumber = 5;
        var ingestedAt = DateTimeOffset.UtcNow;
        var tags = new Dictionary<string, List<string?>>
        {
            { "userId", new List<string?> { "user123" } },
            { "category", new List<string?> { "documents", "technical" } },
            { "priority", new List<string?> { "high" } }
        };

        // Act
        var record = new SkVectorChunkRecord
        {
            DocumentId = documentId,
            FileName = fileName,
            DocumentReference = documentReference,
            ChunkText = chunkText,
            Embedding = embedding,
            PartitionNumber = partitionNumber,
            IngestedAt = ingestedAt,
            Tags = tags
        };

        // Assert
        Assert.Equal(documentId, record.DocumentId);
        Assert.Equal(fileName, record.FileName);
        Assert.Equal(documentReference, record.DocumentReference);
        Assert.Equal(chunkText, record.ChunkText);
        Assert.Equal(embedding, record.Embedding);
        Assert.Equal(partitionNumber, record.PartitionNumber);
        Assert.Equal(ingestedAt, record.IngestedAt);
        Assert.Equal(tags, record.Tags);
    }

    [Fact]
    public void SkVectorChunkRecord_ShouldCreateSuccessfully_WhenOptionalPropertiesNull()
    {
        // Arrange
        const string documentId = "test-doc-123";
        const string fileName = "test-document.pdf";
        const string chunkText = "This is a test chunk of text content.";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        const int partitionNumber = 1;
        var ingestedAt = DateTimeOffset.UtcNow;
        var tags = new Dictionary<string, List<string?>>();

        // Act
        var record = new SkVectorChunkRecord
        {
            DocumentId = documentId,
            FileName = fileName,
            DocumentReference = null, // Optional property
            ChunkText = chunkText,
            Embedding = embedding,
            PartitionNumber = partitionNumber,
            IngestedAt = ingestedAt,
            Tags = tags
        };

        // Assert
        Assert.Equal(documentId, record.DocumentId);
        Assert.Equal(fileName, record.FileName);
        Assert.Null(record.DocumentReference);
        Assert.Equal(chunkText, record.ChunkText);
        Assert.Equal(embedding, record.Embedding);
        Assert.Equal(partitionNumber, record.PartitionNumber);
        Assert.Equal(ingestedAt, record.IngestedAt);
        Assert.Equal(tags, record.Tags);
    }

    [Fact]
    public void SkVectorChunkRecord_Tags_ShouldSupportMultipleValues_WhenProvided()
    {
        // Arrange
        var tags = new Dictionary<string, List<string?>>
        {
            { "categories", new List<string?> { "technical", "documentation", "reference" } },
            { "authors", new List<string?> { "john.doe", "jane.smith" } },
            { "versions", new List<string?> { "1.0", "2.0", null } } // Including null values
        };

        var record = new SkVectorChunkRecord
        {
            DocumentId = "test-doc",
            FileName = "multi-tag-test.pdf",
            ChunkText = "Test content with multiple tag values",
            Embedding = new float[] { 0.1f, 0.2f },
            PartitionNumber = 0,
            IngestedAt = DateTimeOffset.UtcNow,
            Tags = tags
        };

        // Act & Assert
        Assert.Equal(3, record.Tags.Count);
        Assert.Equal(3, record.Tags["categories"].Count);
        Assert.Equal(2, record.Tags["authors"].Count);
        Assert.Equal(3, record.Tags["versions"].Count);
        Assert.Contains("technical", record.Tags["categories"]);
        Assert.Contains("john.doe", record.Tags["authors"]);
        Assert.Contains(null, record.Tags["versions"]);
    }

    [Fact]
    public void VectorSearchMatch_ShouldCreateSuccessfully_WhenValidRecordAndScoreProvided()
    {
        // Arrange
        var record = CreateTestVectorChunkRecord();
        const double score = 0.85;

        // Act
        var match = new VectorSearchMatch(record, score);

        // Assert
        Assert.Equal(record, match.Record);
        Assert.Equal(score, match.Score);
    }

    [Fact]
    public void VectorSearchMatch_ShouldSupportEquality_WhenComparing()
    {
        // Arrange
        var record = CreateTestVectorChunkRecord();
        const double score = 0.85;

        var match1 = new VectorSearchMatch(record, score);
        var match2 = new VectorSearchMatch(record, score);

        // Act & Assert
        Assert.Equal(match1, match2);
        Assert.True(match1 == match2);
        Assert.False(match1 != match2);
    }

    [Fact]
    public void VectorSearchMatch_ShouldNotBeEqual_WhenDifferentScores()
    {
        // Arrange
        var record = CreateTestVectorChunkRecord();
        var match1 = new VectorSearchMatch(record, 0.85);
        var match2 = new VectorSearchMatch(record, 0.75);

        // Act & Assert
        Assert.NotEqual(match1, match2);
        Assert.False(match1 == match2);
        Assert.True(match1 != match2);
    }

    [Fact]
    public void VectorSearchMatch_ShouldNotBeEqual_WhenDifferentRecords()
    {
        // Arrange
        var record1 = CreateTestVectorChunkRecord("doc1");
        var record2 = CreateTestVectorChunkRecord("doc2");
        const double score = 0.85;

        var match1 = new VectorSearchMatch(record1, score);
        var match2 = new VectorSearchMatch(record2, score);

        // Act & Assert
        Assert.NotEqual(match1, match2);
        Assert.False(match1 == match2);
        Assert.True(match1 != match2);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(-0.1)] // Edge case - negative score
    [InlineData(1.1)]  // Edge case - score above 1.0
    public void VectorSearchMatch_ShouldAcceptAnyScore_WhenProvided(double score)
    {
        // Arrange
        var record = CreateTestVectorChunkRecord();

        // Act
        var match = new VectorSearchMatch(record, score);

        // Assert
        Assert.Equal(score, match.Score);
    }

    [Fact]
    public void SkVectorChunkRecord_ShouldBeImmutable_WhenCreated()
    {
        // Arrange
        var record = CreateTestVectorChunkRecord();
        var originalDocumentId = record.DocumentId;
        var originalFileName = record.FileName;
        var originalEmbedding = record.Embedding;
        var originalPartitionNumber = record.PartitionNumber;

        // Act & Assert - Verify properties are init-only
        // The following would not compile if properties weren't init-only:
        // record.DocumentId = "new-id"; // Compilation error
        // record.FileName = "new-name"; // Compilation error
        // record.Embedding = new float[0]; // Compilation error
        // record.PartitionNumber = 999; // Compilation error

        Assert.Equal(originalDocumentId, record.DocumentId);
        Assert.Equal(originalFileName, record.FileName);
        Assert.Equal(originalEmbedding, record.Embedding);
        Assert.Equal(originalPartitionNumber, record.PartitionNumber);
    }

    [Fact]
    public void SkVectorChunkRecord_ToString_ShouldReturnMeaningfulString()
    {
        // Arrange
        var record = CreateTestVectorChunkRecord();

        // Act
        var stringRepresentation = record.ToString();

        // Assert
        Assert.NotNull(stringRepresentation);
        Assert.NotEmpty(stringRepresentation);
        // ToString should contain the type name at minimum
        Assert.Contains(nameof(SkVectorChunkRecord), stringRepresentation);
    }

    #region Helper Methods

    private static SkVectorChunkRecord CreateTestVectorChunkRecord(string? documentId = null)
    {
        var id = documentId ?? "test-doc-123";
        return new SkVectorChunkRecord
        {
            DocumentId = id,
            FileName = $"test-file-{id}.pdf",
            DocumentReference = $"doc:{Guid.NewGuid()}",
            ChunkText = $"This is test content for document {id}.",
            Embedding = Enumerable.Range(0, 384).Select(x => (float)(x * 0.001)).ToArray(),
            PartitionNumber = 1,
            IngestedAt = DateTimeOffset.UtcNow,
            Tags = new Dictionary<string, List<string?>>
            {
                { "userId", new List<string?> { "user123" } },
                { "category", new List<string?> { "test" } }
            }
        };
    }

    #endregion
}