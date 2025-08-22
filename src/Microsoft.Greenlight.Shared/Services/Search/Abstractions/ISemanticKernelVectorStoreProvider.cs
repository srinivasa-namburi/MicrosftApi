// Copyright (c) Microsoft Corporation. All rights reserved.

// using Microsoft.Extensions.VectorData; // Intentionally not referenced directly to avoid unneeded dependency in abstraction.

namespace Microsoft.Greenlight.Shared.Services.Search.Abstractions;

/// <summary>
/// Abstraction over Semantic Kernel vector store operations using official connector collections.
/// </summary>
public interface ISemanticKernelVectorStoreProvider
{
    /// <summary>
    /// Ensures the underlying collection (index) exists and is ready.
    /// </summary>
    Task EnsureCollectionAsync(string indexName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts (adds or updates) a batch of chunk records.
    /// </summary>
    Task UpsertAsync(string indexName, IEnumerable<SkVectorChunkRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all records belonging to a file within an index.
    /// </summary>
    Task DeleteFileAsync(string indexName, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a vector search against the specified index returning scored matches.
    /// Provider is responsible for applying <paramref name="minRelevance"/> and optional exact-match tag filtering.
    /// </summary>
    /// <param name="indexName">Index / collection name.</param>
    /// <param name="queryEmbedding">Embedding for query.</param>
    /// <param name="top">Maximum results.</param>
    /// <param name="minRelevance">Minimum relevance threshold.</param>
    /// <param name="parametersExactMatch">Optional exact-match tag filters (key/value).</param>
    Task<IReadOnlyList<VectorSearchMatch>> SearchAsync(string indexName, float[] queryEmbedding, int top, double minRelevance, Dictionary<string,string>? parametersExactMatch = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all chunk records for a given document within an index. Used to materialize adjacent partitions
    /// (preceding/following) around initially matched partitions when constructing consolidated citations.
    /// Implementations may perform an optimized point lookup or a broad scan depending on connector capabilities.
    /// </summary>
    Task<IReadOnlyList<SkVectorChunkRecord>> GetAllDocumentChunksAsync(string indexName, string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to retrieve a single chunk by its composite key (documentId + partitionNumber).
    /// Returns null if not found or connector doesn't support direct key lookup.
    /// </summary>
    Task<SkVectorChunkRecord?> TryGetChunkAsync(string indexName, string documentId, int partitionNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves (and optionally caches) the ordered list of partition numbers for a document.
    /// Used to cheaply compute neighbor existence without loading all chunk payloads.
    /// </summary>
    Task<IReadOnlyList<int>> GetDocumentPartitionNumbersAsync(string indexName, string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves neighbor chunks using explicit asymmetric window sizes.
    /// Does NOT return the base partition itself; only neighbors within the specified preceding / following ranges.
    /// </summary>
    Task<IReadOnlyList<SkVectorChunkRecord>> GetNeighborChunksAsync(string indexName, string documentId, int partitionNumber, int precedingPartitions, int followingPartitions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy radius-based neighbor retrieval (symmetric). Retained for backward compatibility.
    /// </summary>
    [Obsolete("Use GetNeighborChunksAsync with explicit preceding/following counts")]
    Task<IReadOnlyList<SkVectorChunkRecord>> GetNeighborChunksAsync(string indexName, string documentId, int partitionNumber, int radius, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the entire collection/index for the specified name. If the collection doesn't exist, the call is a no-op.
    /// Implementations should prefer an efficient provider-level deletion rather than per-record deletion.
    /// </summary>
    Task ClearCollectionAsync(string indexName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Scored match wrapper for search results.
/// </summary>
public sealed record VectorSearchMatch(SkVectorChunkRecord Record, double Score);
