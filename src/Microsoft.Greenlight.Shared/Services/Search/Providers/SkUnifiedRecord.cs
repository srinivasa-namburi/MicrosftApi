// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.VectorData;
using System.Text.Json; // For JSON serialization of tags

namespace Microsoft.Greenlight.Shared.Services.Search.Providers;

/// <summary>
/// Internal SK annotated record type used by the concrete SemanticKernelUnifiedVectorStoreProvider
/// for both Postgres (pgvector) and Azure AI Search collections. Not exposed outside provider layer.
/// </summary>
internal sealed class SkUnifiedRecord
{
    // Primary key for the chunk (documentId:partitionNumber) enables O(1) point retrieval when connector supports GetAsync.
    [VectorStoreKey]
    public required string ChunkId { get; init; }

    // Filterable/indexed metadata for grouping & doc-level fetch.
    [VectorStoreData(IsIndexed = true)]
    public required string DocumentId { get; init; }

    // Filterable for file-scoped deletes and secondary index invalidation.
    [VectorStoreData(IsIndexed = true)]
    public required string FileName { get; init; }

    // Not full-text; simple equality queries only (leave IsIndexed true for future filter support if needed).
    [VectorStoreData(IsIndexed = true)]
    public string? OriginalDocumentUrl { get; init; }

    // Full text for optional hybrid search / future metadata prefilter.
    [VectorStoreData(IsIndexed = false, IsFullTextIndexed = false)]
    public required string ChunkText { get; init; }

    // Vector field - dimensions fixed by current embedding model.
    // Make optional for search result deserialization when vectors are excluded by the connector.
    [VectorStoreVector(1536)]
    public float[]? Embedding { get; init; }

    // Critical for adjacency: mark indexed so future connector filtering (PartitionNumber IN (...)) is possible.
    [VectorStoreData(IsIndexed = true)]
    public required int PartitionNumber { get; init; }

    // Storage for ingestion timestamp - use DateTimeOffset to satisfy Azure AI Search (Edm.DateTimeOffset)
    // and remain compatible with Postgres connector. Prefer UTC offset.
    [VectorStoreData(IsIndexed = true)]
    public DateTimeOffset IngestedAt { get; init; }

    // Flattened tags (Dictionary<string,List<string?>>) serialized as JSON to satisfy connector scalar/list type constraints.
    // Consumers should deserialize back to original structure; provider mapping handles this.
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = false)]
    public required string TagsJson { get; init; }

    // Helper to convert dictionary to JSON deterministically (stable ordering for caching / equality checks if needed).
    public static string SerializeTags(Dictionary<string, List<string?>> tags)
    {
        var ordered = tags
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(ordered);
    }
}
