// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

/// <summary>
/// Represents a source reference item produced by the Semantic Kernel Vector Store implementation.
/// </summary>
public class VectorStoreSourceReferenceItem : SourceReferenceItem
{
    /// <summary>
    /// The index (collection) searched.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the originating document (assigned during ingestion).
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Original file name used during ingestion.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Highest chunk similarity score associated with this grouping.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Flattened text output (aggregated chunk text) for convenience.
    /// </summary>
    public override string? SourceOutput { get; set; }

    /// <summary>
    /// Chunks returned for this reference (ordered by relevance or partition number). Not persisted by design.
    /// </summary>
    public List<DocumentChunk> Chunks { get; set; } = new();

    /// <summary>
    /// Persisted list of chunk identifiers (partition numbers) captured at creation time.
    /// Stored as a comma-separated list to allow lazy rehydration from the vector store without eager chunk payloads.
    /// </summary>
    public string? StoredPartitionNumbers { get; set; }

    /// <inheritdoc />
    public override void SetBasicParameters()
    {
        // Using GeneralKnowledge as a broad classification for vector store aggregated fragments.
        SourceReferenceType = SourceReferenceType.GeneralKnowledge;
        Description = "Document fragments from Semantic Kernel Vector Store";
    }
}
