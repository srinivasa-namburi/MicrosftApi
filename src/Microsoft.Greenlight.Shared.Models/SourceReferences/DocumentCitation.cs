// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel.DataAnnotations.Schema;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

/// <summary>
/// Represents a document chunk/partition retrieved from a document repository.
/// Generic equivalent to Kernel Memory's Citation.Partition.
/// </summary>
[NotMapped]
public class DocumentChunk
{
    /// <summary>
    /// Text content of the document chunk.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Relevance score of this chunk to the search query.
    /// </summary>
    public double Relevance { get; set; }

    /// <summary>
    /// Partition/chunk number within the document.
    /// </summary>
    public int PartitionNumber { get; set; }

    /// <summary>
    /// Size of the text content in bytes.
    /// </summary>
    public int SizeInBytes { get; set; }

    /// <summary>
    /// Metadata tags associated with this chunk.
    /// </summary>
    public Dictionary<string, List<string?>> Tags { get; set; } = new();

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTimeOffset LastUpdate { get; set; }
}

/// <summary>
/// Represents a document citation containing multiple chunks.
/// Generic equivalent to Kernel Memory's Citation.
/// </summary>
[NotMapped]
public class DocumentCitation
{
    /// <summary>
    /// Link to the source document.
    /// </summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// Index where this document is stored.
    /// </summary>
    public string Index { get; set; } = string.Empty;

    /// <summary>
    /// Document identifier.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// File identifier.
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// List of document chunks/partitions.
    /// </summary>
    public List<DocumentChunk> Partitions { get; set; } = new();

    /// <summary>
    /// Metadata tags associated with this document.
    /// </summary>
    public Dictionary<string, List<string?>> Tags { get; set; } = new();
}

/// <summary>
/// Represents an answer from a document repository.
/// Generic equivalent to Kernel Memory's MemoryAnswer.
/// </summary>
[NotMapped]
public class DocumentRepositoryAnswer
{
    /// <summary>
    /// The answer text.
    /// </summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// Relevance score of the answer.
    /// </summary>
    public double Relevance { get; set; }

    /// <summary>
    /// Source citations used to generate the answer.
    /// </summary>
    public List<DocumentCitation> RelevantSources { get; set; } = new();

    /// <summary>
    /// Indicates if the answer has any citations.
    /// </summary>
    [NotMapped]
    public bool HasRelevantSources => RelevantSources.Count > 0;
}