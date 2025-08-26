// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using StackExchange.Redis;
using Microsoft.Greenlight.Shared.Services.Caching;

namespace Microsoft.Greenlight.Shared.Services.Search.Providers;

public sealed class SemanticKernelVectorStoreProvider : ISemanticKernelVectorStoreProvider
{
    private readonly ILogger<SemanticKernelVectorStoreProvider> _logger;
    private readonly VectorStoreOptions _options;
    private readonly IConnectionMultiplexer? _redis;
    private readonly VectorStore _vectorStore;
    private readonly IAppCache? _appCache;

    private const string RedisFileIndexKeyPrefix = "SkFileDocIndex";
    private const string RedisDocPartitionsKeyPrefix = "SkDocPartitions";
    private const string AppCacheFileIndexKeyPrefix = "sk:filedocindex";
    private const string AppCacheDocPartitionsKeyPrefix = "sk:docparts";
    private TimeSpan DocumentChunkCacheTtl => TimeSpan.FromMinutes(_options.DocumentChunkCacheTtlMinutes <= 0 ? 30 : _options.DocumentChunkCacheTtlMinutes);

    /// <summary>
    /// Creates a new Semantic Kernel vector store provider that uses the configured VectorStore.
    /// </summary>
    public SemanticKernelVectorStoreProvider(
        ILogger<SemanticKernelVectorStoreProvider> logger,
        IOptionsMonitor<ServiceConfigurationOptions> rootOptionsMonitor,
        VectorStore vectorStore,
        IConnectionMultiplexer? redis = null,
        IAppCache? appCache = null)
    {
        _logger = logger;
        _options = rootOptionsMonitor.CurrentValue.GreenlightServices.VectorStore;
        _vectorStore = vectorStore;
        _redis = redis;
        _appCache = appCache;
    }

    private VectorStoreCollection<string, SkUnifiedRecord> GetCollection(string indexName, VectorStoreCollectionDefinition? definition = null)
    {
        var norm = Normalize(indexName);
        return _vectorStore.GetCollection<string, SkUnifiedRecord>(norm, definition);
    }

    /// <summary>
    /// Build a record definition for SkUnifiedRecord with a dynamic vector dimension for the Embedding property.
    /// Other properties mirror the attributes on SkUnifiedRecord.
    /// </summary>
    private static VectorStoreCollectionDefinition BuildSkUnifiedDefinition(int dimensions)
    {
        var def = new VectorStoreCollectionDefinition();
        // Key
        def.Properties.Add(new VectorStoreKeyProperty("ChunkId", typeof(string)));

        // Data properties (align with attributes on SkUnifiedRecord)
        def.Properties.Add(new VectorStoreDataProperty("DocumentId", typeof(string)) { IsIndexed = true });
        def.Properties.Add(new VectorStoreDataProperty("FileName", typeof(string)) { IsIndexed = true });
        def.Properties.Add(new VectorStoreDataProperty("OriginalDocumentUrl", typeof(string)) { IsIndexed = true, IsFullTextIndexed = false });
        def.Properties.Add(new VectorStoreDataProperty("ChunkText", typeof(string)) { IsIndexed = false, IsFullTextIndexed = false });
        def.Properties.Add(new VectorStoreDataProperty("PartitionNumber", typeof(int)) { IsIndexed = true });
        def.Properties.Add(new VectorStoreDataProperty("IngestedAt", typeof(DateTimeOffset)) { IsIndexed = true });
        def.Properties.Add(new VectorStoreDataProperty("TagsJson", typeof(string)) { IsIndexed = true, IsFullTextIndexed = false });

        // Explicit type must match SkUnifiedRecord.Embedding (float[]), and we set dynamic dimensions
        var vector = new VectorStoreVectorProperty("Embedding", typeof(float[]), dimensions);
        def.Properties.Add(vector);

        return def;
    }

    /// <inheritdoc />
    public async Task EnsureCollectionAsync(string indexName, int embeddingDimensions, CancellationToken cancellationToken = default)
    {
        // Create collection with explicit dimensions to ensure proper schema
        var definition = BuildSkUnifiedDefinition(embeddingDimensions);
        var collection = GetCollection(indexName, definition);
        try
        {
            await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Ensured collection {Index} exists with {Dimensions} dimensions", indexName, embeddingDimensions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure collection {Index} exists with {Dimensions} dimensions", indexName, embeddingDimensions);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> CollectionExistsAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(indexName);
        try
        {
            return await collection.CollectionExistsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check if collection {Index} exists, assuming false", indexName);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task UpsertAsync(string indexName, IEnumerable<SkVectorChunkRecord> records, CancellationToken cancellationToken = default)
    {
        var list = records?.ToList() ?? new List<SkVectorChunkRecord>();
        if (list.Count == 0)
        {
            return;
        }

        // Determine effective dimensions from the first embedding; fallback to configured default
        var dims = list.First().Embedding?.Length ?? (_options.VectorSize > 0 ? _options.VectorSize : 1536);
        var definition = BuildSkUnifiedDefinition(dims);
        var collection = GetCollection(indexName, definition);
        var unifiedRecords = list.Select(Map).ToList();

        try
        {
            await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
            await collection.UpsertAsync(unifiedRecords, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsCollectionNotFoundError(ex))
        {
            _logger.LogDebug("Collection {Index} missing, creating with dims={Dims} and retrying upsert", indexName, dims);
            await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
            await collection.UpsertAsync(unifiedRecords, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Help diagnose vector dimension mismatches from backing providers (e.g., Azure AI Search)
            var msg = ex.ToString();
            if (msg.IndexOf("mismatch in vector dimensions", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("expects a length of", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger.LogError(ex, "Vector dimension mismatch while upserting into {Index}. ProvidedDims={ProvidedDims}. The existing collection likely uses a different dimension. Clear the collection or use a consistent embedding model.", indexName, dims);
                throw new InvalidOperationException($"Vector dimension mismatch for index '{indexName}'. Provided embeddings have dimension {dims}, but the collection expects a different dimension. Clear the collection or use a consistent embedding model.", ex);
            }
            _logger.LogError(ex, "Failed to upsert into collection {Index}", indexName);
            throw;
        }

        // Update caches: partition list by documentId and optional fileName->docId index
        var normUpsert = Normalize(indexName);
        foreach (var grp in list.GroupBy(r => r.DocumentId))
        {
            var orderedChunks = grp.OrderBy(c => c.PartitionNumber).ToList();
            var partitions = orderedChunks.Select(o => o.PartitionNumber).ToList();
            try
            {
                // Prefer in-process cache to avoid pressure on Redis for small hot items
                if (_appCache != null)
                {
                    var appKey = BuildAppDocPartitionsKey(normUpsert, grp.Key);
                    await _appCache.SetAsync(appKey, partitions, DocumentChunkCacheTtl, allowDistributed: false, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to set in-memory cache for document partitions for {Index}/{Doc}", normUpsert, grp.Key);
            }

            // Best-effort Redis set remains for cross-process hints (values are tiny lists of ints)
            if (_redis != null)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    _ = db.StringSetAsync(BuildDocPartitionsKey(normUpsert, grp.Key), JsonSerializer.Serialize(partitions), DocumentChunkCacheTtl);
                    if (_options.EnableFileNameDocIdCacheIndex)
                    {
                        var fileName = orderedChunks.First().FileName;
                        _ = db.StringSetAsync(BuildFileIndexKey(normUpsert, fileName), grp.Key, DocumentChunkCacheTtl);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to cache document partitions in Redis for {Index}/{Doc}", normUpsert, grp.Key);
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteFileAsync(string indexName, string fileName, CancellationToken cancellationToken = default)
    {
        // If collection doesn't exist, treat as no-op; do not create with unknown dimensions here.
        var collection = GetCollection(indexName);
        var norm = Normalize(indexName);

        try
        {
            Expression<Func<SkUnifiedRecord, bool>> filter = record => record.FileName == fileName;
            var recordsToDelete = new List<string>();

            await foreach (var record in collection.GetAsync(filter, top: int.MaxValue, options: null, cancellationToken))
            {
                recordsToDelete.Add(record.ChunkId);
            }

            if (recordsToDelete.Count > 0)
            {
                await collection.DeleteAsync(recordsToDelete, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Deleted {Count} chunks for file {FileName} from index {Index}", recordsToDelete.Count, fileName, indexName);

                // Invalidate caches (best-effort)
                string? documentId = null;

                // Try resolve via Redis first if enabled (for cross-process correctness)
                if (_redis != null && _options.EnableFileNameDocIdCacheIndex)
                {
                    var db = _redis.GetDatabase();
                    try
                    {
                        var val = await db.StringGetAsync(BuildFileIndexKey(norm, fileName)).ConfigureAwait(false);
                        if (val.HasValue)
                        {
                            documentId = val!;
                            _ = db.KeyDeleteAsync(BuildFileIndexKey(norm, fileName));
                            _ = db.KeyDeleteAsync(BuildDocPartitionsKey(norm, documentId!));
                            _logger.LogDebug("Invalidated Redis cache for file {FileName} and document {DocumentId}", fileName, documentId);
                        }
                        else
                        {
                            _ = db.KeyDeleteAsync(BuildFileIndexKey(norm, fileName));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to invalidate Redis cache for file {FileName}", fileName);
                    }
                }

                // Invalidate in-process cache if we can resolve a key
                try
                {
                    if (_appCache != null)
                    {
                        await _appCache.RemoveAsync(BuildAppFileIndexKey(norm, fileName), cancellationToken).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(documentId))
                        {
                            await _appCache.RemoveAsync(BuildAppDocPartitionsKey(norm, documentId!), cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to invalidate in-memory cache for file {FileName}", fileName);
                }
            }
            else
            {
                _logger.LogDebug("No chunks found for file {FileName} in index {Index}", fileName, indexName);
            }
        }
        catch (Exception ex) when (IsCollectionNotFoundError(ex))
        {
            _logger.LogDebug("Collection {Index} not found for DeleteFile; treating as no-op", indexName);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {FileName} from index {Index}", fileName, indexName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VectorSearchMatch>> SearchAsync(string indexName, float[] queryEmbedding, int top, double minRelevance, Dictionary<string, string>? parametersExactMatch = null, CancellationToken cancellationToken = default)
    {
        // Build a definition with dimensions derived from the query embedding
        var dims = queryEmbedding?.Length ?? (_options.VectorSize > 0 ? _options.VectorSize : 1536);
        var definition = BuildSkUnifiedDefinition(dims);
        var collection = GetCollection(indexName, definition);
        IAsyncEnumerable<VectorSearchResult<SkUnifiedRecord>> results;

        try
        {
            await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
            results = collection.SearchAsync(
                queryEmbedding,
                top,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (IsCollectionNotFoundError(ex))
        {
            _logger.LogDebug("Collection {Index} doesn't exist for search, creating with dims={Dims}", indexName, dims);
            await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
            results = collection.SearchAsync(
                queryEmbedding,
                top,
                cancellationToken: cancellationToken);
        }

        var matches = new List<VectorSearchMatch>(top);
        await foreach (var r in results.WithCancellation(cancellationToken))
        {
            if (r.Score.HasValue && r.Score.Value >= minRelevance)
            {
                matches.Add(new VectorSearchMatch(Unmap(r.Record), r.Score.Value));
            }
        }

        if (parametersExactMatch is { Count: > 0 })
        {
            matches = matches
                .Where(m => parametersExactMatch.All(f => m.Record.Tags.TryGetValue(f.Key, out var vals) && vals.Any(v => string.Equals(v, f.Value, StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }

        return matches;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkVectorChunkRecord>> GetAllDocumentChunksAsync(string indexName, string documentId, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(indexName);
        var results = new List<SkVectorChunkRecord>();

        try
        {
            Expression<Func<SkUnifiedRecord, bool>> filter = record => record.DocumentId == documentId;

            await foreach (var record in collection.GetAsync(filter, top: int.MaxValue, options: null, cancellationToken))
            {
                results.Add(Unmap(record));
            }
        }
        catch (Exception ex) when (IsCollectionNotFoundError(ex))
        {
            _logger.LogDebug("Collection {Index} not found for GetAllDocumentChunks; returning empty", indexName);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all chunks for document {DocumentId} from index {Index}", documentId, indexName);
            throw;
        }

        results.Sort((a, b) => a.PartitionNumber.CompareTo(b.PartitionNumber));

        _logger.LogDebug("Retrieved {Count} chunks for document {DocumentId} from index {Index}", results.Count, documentId, indexName);
        return results;
    }

    /// <inheritdoc />
    public async Task<SkVectorChunkRecord?> TryGetChunkAsync(string indexName, string documentId, int partitionNumber, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(indexName);
        var chunkId = $"{documentId}={partitionNumber}";

        try
        {
            // For neighbor expansion and lookups, vectors are not required; avoid loading large payloads.
            var rec = await collection.GetAsync(chunkId, new RecordRetrievalOptions { IncludeVectors = false }, cancellationToken).ConfigureAwait(false);
            return rec != null ? Unmap(rec) : null;
        }
        catch (Exception ex) when (IsCollectionNotFoundError(ex))
        {
            _logger.LogDebug("Collection {Index} not found for TryGetChunk; returning null", indexName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Chunk point lookup failed for {ChunkId} (index {Index})", chunkId, indexName);
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> GetDocumentPartitionNumbersAsync(string indexName, string documentId, CancellationToken cancellationToken = default)
    {
        var norm = Normalize(indexName);

        // Prefer in-process cache for hot-path partition lookups
        if (_appCache != null)
        {
            try
            {
                var appKey = BuildAppDocPartitionsKey(norm, documentId);
                var nums = await _appCache.GetOrCreateAsync(appKey, async _ =>
                {
                    // Populate from Redis if available, else from vector store
                    var fallback = await TryGetPartitionsFromRedisAsync(norm, documentId).ConfigureAwait(false);
                    if (fallback != null && fallback.Count > 0)
                    {
                        return fallback;
                    }
                    var chunks = await GetAllDocumentChunksAsync(indexName, documentId, cancellationToken).ConfigureAwait(false);
                    return chunks.Select(c => c.PartitionNumber).OrderBy(n => n).ToList();
                }, DocumentChunkCacheTtl, allowDistributed: false, cancellationToken).ConfigureAwait(false);
                if (nums.Count > 0)
                {
                    return nums;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "In-memory cache failure for partition list {Doc} in {Index}", documentId, norm);
            }
        }

        // Fallback to Redis-only quick check
        var redisNums = await TryGetPartitionsFromRedisAsync(norm, documentId).ConfigureAwait(false);
        if (redisNums != null && redisNums.Count > 0)
        {
            return redisNums;
        }

        var chunksFallback = await GetAllDocumentChunksAsync(indexName, documentId, cancellationToken).ConfigureAwait(false);
        return chunksFallback.Select(c => c.PartitionNumber).OrderBy(n => n).ToList();
    }

    private async Task<List<int>?> TryGetPartitionsFromRedisAsync(string normIndex, string documentId)
    {
        if (_redis == null)
        {
            return null;
        }

        try
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync(BuildDocPartitionsKey(normIndex, documentId)).ConfigureAwait(false);
            if (cached.HasValue)
            {
                var nums = JsonSerializer.Deserialize<List<int>>(cached!) ?? new();
                if (nums.Count > 0)
                {
                    return nums;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get partition list from Redis for {Doc} in {Index}", documentId, normIndex);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkVectorChunkRecord>> GetNeighborChunksAsync(string indexName, string documentId, int partitionNumber, int precedingPartitions, int followingPartitions, CancellationToken cancellationToken = default)
    {
        if (precedingPartitions < 0)
        {
            precedingPartitions = 0;
        }
        if (followingPartitions < 0)
        {
            followingPartitions = 0;
        }
        if (precedingPartitions == 0 && followingPartitions == 0)
        {
            return Array.Empty<SkVectorChunkRecord>();
        }

        // Avoid scanning all partitions: probe exact neighbor keys directly (O(radius))
        var results = new List<SkVectorChunkRecord>(precedingPartitions + followingPartitions);
        var min = partitionNumber - precedingPartitions;
        var max = partitionNumber + followingPartitions;
        for (var p = min; p <= max; p++)
        {
            if (p == partitionNumber)
            {
                continue;
            }
            var chunk = await TryGetChunkAsync(indexName, documentId, p, cancellationToken).ConfigureAwait(false);
            if (chunk != null)
            {
                results.Add(chunk);
            }
        }

        return results.OrderBy(r => r.PartitionNumber).ToList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SkVectorChunkRecord>> GetNeighborChunksAsync(string indexName, string documentId, int partitionNumber, int radius, CancellationToken cancellationToken = default)
        => GetNeighborChunksAsync(indexName, documentId, partitionNumber, radius, radius, cancellationToken);

    /// <inheritdoc />
    public async Task ClearCollectionAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(indexName);
        try
        {
            await collection.EnsureCollectionDeletedAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Cleared (deleted) collection {Index}. Cache entries (if any) will expire naturally (TTL={TtlMinutes}m)", indexName, DocumentChunkCacheTtl.TotalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete collection {Index}", indexName);
            throw;
        }
    }

    private static SkUnifiedRecord Map(SkVectorChunkRecord r) => new()
    {
        ChunkId = $"{r.DocumentId}={r.PartitionNumber}",
        DocumentId = r.DocumentId,
        FileName = r.FileName,
        OriginalDocumentUrl = r.OriginalDocumentUrl,
        ChunkText = r.ChunkText,
        Embedding = r.Embedding,
        PartitionNumber = r.PartitionNumber,
        IngestedAt = r.IngestedAt.ToUniversalTime(),
        TagsJson = SkUnifiedRecord.SerializeTags(r.Tags)
    };

    private static SkVectorChunkRecord Unmap(SkUnifiedRecord r) => new()
    {
        DocumentId = r.DocumentId,
        FileName = r.FileName,
        OriginalDocumentUrl = r.OriginalDocumentUrl,
        ChunkText = r.ChunkText,
        Embedding = r.Embedding ?? Array.Empty<float>(),
        PartitionNumber = r.PartitionNumber,
        IngestedAt = r.IngestedAt.ToUniversalTime(),
        Tags = JsonSerializer.Deserialize<Dictionary<string, List<string?>>>(r.TagsJson) ?? new(StringComparer.OrdinalIgnoreCase)
    };

    /// <summary>
    /// Determines if an exception indicates that a collection/index doesn't exist.
    /// Different vector store providers may throw different exceptions for missing collections.
    /// Handles wrapped exceptions (e.g., VectorStoreException -> provider-specific exception).
    /// </summary>
    private static bool IsCollectionNotFoundError(Exception ex)
    {
        Exception cur = ex;
        while (cur.InnerException != null)
        {
            cur = cur.InnerException;
        }

        var full = (ex.ToString() + "\n" + cur.Message).ToLowerInvariant();

        if (full.Contains("does not exist") || full.Contains("not exist") || full.Contains("not found"))
        {
            if (full.Contains("collection") || full.Contains("index") || full.Contains("relation") || full.Contains("table"))
            {
                return true;
            }
        }

        if (full.Contains("42p01"))
        {
            return true;
        }

        return ex is ArgumentException || ex is InvalidOperationException;
    }

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();
    private static string BuildFileIndexKey(string normIndex, string fileName) => $"{RedisFileIndexKeyPrefix}:{normIndex}:{fileName}";
    private static string BuildDocPartitionsKey(string normIndex, string documentId) => $"{RedisDocPartitionsKeyPrefix}:{normIndex}:{documentId}";
    private static string BuildAppFileIndexKey(string normIndex, string fileName) => $"{AppCacheFileIndexKeyPrefix}:{normIndex}:{fileName}";
    private static string BuildAppDocPartitionsKey(string normIndex, string documentId) => $"{AppCacheDocPartitionsKeyPrefix}:{normIndex}:{documentId}";
}
