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

    // Document reference for dynamic URL resolution (replaces OriginalDocumentUrl)
    // Stores identifier that can be resolved to proxied URL at search time
    [VectorStoreData(IsIndexed = true)]
    public string? DocumentReference { get; init; }

    // Full text for optional hybrid search / future metadata prefilter.
    [VectorStoreData(IsIndexed = false, IsFullTextIndexed = false)]
    public required string ChunkText { get; init; }

    // Vector field - attribute dimension (1536) is a fallback ONLY.
    // Our provider always supplies an explicit VectorStoreCollectionDefinition
    // with the correct embedding dimension at runtime (per index/model),
    // which overrides this attribute. This enables multiple embedding sizes
    // across different collections, including content reference indexes.
    // The property remains optional for cases where connectors omit vectors
    // on retrieval (e.g., point lookups or neighbor expansion).
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
