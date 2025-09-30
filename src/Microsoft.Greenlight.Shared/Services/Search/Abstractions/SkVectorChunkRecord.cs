// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Services.Search.Abstractions;

/// <summary>
/// Canonical vector chunk record used by higher-level ingestion/search code.
/// This is a storage-agnostic representation independent of specific SK connector attributes.
/// </summary>
public sealed class SkVectorChunkRecord
{
    /// <summary>Document identifier grouping all partitions.</summary>
    public required string DocumentId { get; init; }
    /// <summary>Original file name.</summary>
    public required string FileName { get; init; }
    /// <summary>UI-friendly display filename for user presentation. Null for non-file-based records.</summary>
    public string? DisplayFileName { get; init; }
    /// <summary>Reference to FileAcknowledgmentRecord for file-based records. Null for non-file-based records.</summary>
    public Guid? FileAcknowledgmentRecordId { get; init; }
    /// <summary>Document reference for dynamic URL resolution (replaces OriginalDocumentUrl).</summary>
    public string? DocumentReference { get; init; }
    /// <summary>Chunk raw text content.</summary>
    public required string ChunkText { get; init; }
    /// <summary>Embedding vector for the chunk.</summary>
    public required float[] Embedding { get; init; }
    /// <summary>Sequential partition number within the document.</summary>
    public required int PartitionNumber { get; init; }
    /// <summary>Timestamp the chunk was ingested.</summary>
    public required DateTimeOffset IngestedAt { get; init; }
    /// <summary>Arbitrary metadata tags (multi-value) preserved for filtering and citation.</summary>
    public required Dictionary<string, List<string?>> Tags { get; init; }
}

