// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Enums;
using StackExchange.Redis;
using Microsoft.Greenlight.Shared.Services.Caching;
using Npgsql;

namespace Microsoft.Greenlight.Shared.Services.Search.Providers;

public sealed class SemanticKernelVectorStoreProvider : ISemanticKernelVectorStoreProvider
{
    private readonly ILogger<SemanticKernelVectorStoreProvider> _logger;
    private readonly VectorStoreOptions _options;
    private readonly IConnectionMultiplexer? _redis;
    private readonly VectorStore _vectorStore;
    private readonly IAppCache? _appCache;
    private readonly IConfiguration _configuration;

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
        IConfiguration configuration,
        IConnectionMultiplexer? redis = null,
        IAppCache? appCache = null)
    {
        _logger = logger;
        _options = rootOptionsMonitor.CurrentValue.GreenlightServices.VectorStore;
        _vectorStore = vectorStore;
        _configuration = configuration;
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
        def.Properties.Add(new VectorStoreDataProperty("DisplayFileName", typeof(string)) { IsIndexed = true });
        def.Properties.Add(new VectorStoreDataProperty("FileAcknowledgmentRecordId", typeof(string)) { IsIndexed = true });
        // Store document reference for dynamic URL resolution instead of fixed URL
        def.Properties.Add(new VectorStoreDataProperty("DocumentReference", typeof(string)) { IsIndexed = true, IsFullTextIndexed = false });
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

            // For PostgreSQL, ensure schema compatibility for new nullable fields
            if (_options.StoreType == VectorStoreType.PostgreSQL)
            {
                await EnsurePostgresSchemaCompatibilityAsync(indexName, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsSchemaCompatibilityException(ex))
        {
            _logger.LogWarning(ex, "Schema compatibility issue for collection {Index}, attempting fallback: {Error}", indexName, ex.Message);

            // For PostgreSQL, try to handle schema issues gracefully
            if (_options.StoreType == VectorStoreType.PostgreSQL)
            {
                await HandlePostgresSchemaCompatibilityAsync(indexName, embeddingDimensions, ex, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // For other providers (AI Search), re-throw as they should handle schema changes automatically
                throw;
            }
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
            _logger.LogDebug("Upserting {Count} records into collection {IndexName}", unifiedRecords.Count, indexName);
            await collection.UpsertAsync(unifiedRecords, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Upsert completed for {Count} records into collection {IndexName}", unifiedRecords.Count, indexName);
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
            if (r.Score.HasValue)
            {
                // Normalize score based on provider type
                var normalizedScore = NormalizeScore(r.Score.Value);

                if (normalizedScore >= minRelevance)
                {
                    matches.Add(new VectorSearchMatch(Unmap(r.Record), normalizedScore));
                }
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
        DisplayFileName = r.DisplayFileName,
        FileAcknowledgmentRecordId = r.FileAcknowledgmentRecordId?.ToString(),
        DocumentReference = r.DocumentReference,
        ChunkText = r.ChunkText,
        Embedding = r.Embedding,
        PartitionNumber = r.PartitionNumber,
        IngestedAt = r.IngestedAt,
        TagsJson = SkUnifiedRecord.SerializeTags(r.Tags)
    };

    private static SkVectorChunkRecord Unmap(SkUnifiedRecord r) => new()
    {
        DocumentId = r.DocumentId,
        FileName = r.FileName,
        DisplayFileName = GetEffectiveDisplayFileName(r.DisplayFileName, r.FileName),
        FileAcknowledgmentRecordId = string.IsNullOrEmpty(r.FileAcknowledgmentRecordId) ? null : Guid.Parse(r.FileAcknowledgmentRecordId),
        DocumentReference = r.DocumentReference,
        ChunkText = r.ChunkText,
        Embedding = r.Embedding ?? Array.Empty<float>(),
        PartitionNumber = r.PartitionNumber,
        IngestedAt = r.IngestedAt,
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

    /// <summary>
    /// Checks if the exception indicates a schema compatibility issue (e.g., missing columns).
    /// </summary>
    private static bool IsSchemaCompatibilityException(Exception ex)
    {
        return ex switch
        {
            PostgresException pgEx when pgEx.SqlState == "42703" => true, // Column doesn't exist
            VectorStoreException vsEx when vsEx.InnerException is PostgresException pgInner && pgInner.SqlState == "42703" => true,
            _ => false
        };
    }

    /// <summary>
    /// Ensures PostgreSQL schema compatibility by adding missing nullable columns.
    /// </summary>
    private async Task EnsurePostgresSchemaCompatibilityAsync(string indexName, CancellationToken cancellationToken)
    {
        try
        {
            // Get the normalized table name that's actually used
            var tableName = Normalize(indexName);

            // For system-controlled indexes, be more aggressive about schema updates
            var isSystemIndex = indexName.StartsWith("system-", StringComparison.OrdinalIgnoreCase);

            var connectionString = GetPostgresConnectionString();
            if (!string.IsNullOrEmpty(connectionString))
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                // Check for multiple missing columns at once
                var missingColumns = await GetMissingColumnsAsync(connection, tableName, cancellationToken);

                if (missingColumns.Count > 0)
                {
                    if (isSystemIndex && missingColumns.Count > 1)
                    {
                        // For system indexes with multiple missing columns, recreate the entire table
                        _logger.LogWarning("System index {IndexName} is missing {Count} columns: {Columns}. Recreating table.", indexName, missingColumns.Count, string.Join(", ", missingColumns));
                        await RecreateTableAsync(connection, tableName, cancellationToken);
                    }
                    else
                    {
                        // Add missing columns individually
                        foreach (var column in missingColumns)
                        {
                            await AddMissingColumnAsync(connection, tableName, column, cancellationToken);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure schema compatibility for PostgreSQL table {IndexName}, will proceed with degraded functionality", indexName);
            // Don't throw - graceful degradation
        }
    }

    /// <summary>
    /// Gets a list of missing columns from the SkUnifiedRecord schema.
    /// </summary>
    private async Task<List<string>> GetMissingColumnsAsync(NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var requiredColumns = new[] { "DisplayFileName", "FileAcknowledgmentRecordId", "DocumentReference" };
        var missingColumns = new List<string>();

        foreach (var column in requiredColumns)
        {
            var checkSql = """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = @tableName AND column_name = @columnName
                );
                """;

            await using var checkCmd = new NpgsqlCommand(checkSql, connection);
            checkCmd.Parameters.AddWithValue("tableName", tableName);
            checkCmd.Parameters.AddWithValue("columnName", column);

            var exists = (bool)(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? false);
            if (!exists)
            {
                missingColumns.Add(column);
            }
        }

        return missingColumns;
    }

    /// <summary>
    /// Adds a single missing column to the PostgreSQL table.
    /// </summary>
    private async Task AddMissingColumnAsync(NpgsqlConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        var alterSql = columnName switch
        {
            "DisplayFileName" => $"""ALTER TABLE "{tableName}" ADD COLUMN "DisplayFileName" TEXT NULL;""",
            "FileAcknowledgmentRecordId" => $"""ALTER TABLE "{tableName}" ADD COLUMN "FileAcknowledgmentRecordId" TEXT NULL;""",
            "DocumentReference" => $"""ALTER TABLE "{tableName}" ADD COLUMN "DocumentReference" TEXT NULL;""",
            _ => null
        };

        if (alterSql != null)
        {
            await using var alterCmd = new NpgsqlCommand(alterSql, connection);
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Added {ColumnName} column to PostgreSQL table {TableName}", columnName, tableName);
        }
    }

    /// <summary>
    /// Recreates the PostgreSQL table by dropping and recreating it.
    /// This is used for system indexes where we control the data entirely.
    /// </summary>
    private async Task RecreateTableAsync(NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        try
        {
            // Drop the existing table (this will also drop any indexes)
            var dropSql = $"""DROP TABLE IF EXISTS "{tableName}" CASCADE;""";
            await using var dropCmd = new NpgsqlCommand(dropSql, connection);
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Dropped and will recreate PostgreSQL table {TableName} for schema compatibility", tableName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not recreate PostgreSQL table {TableName}", tableName);
            throw;
        }
    }

    /// <summary>
    /// Handles PostgreSQL schema compatibility issues during collection creation.
    /// </summary>
    private async Task HandlePostgresSchemaCompatibilityAsync(string indexName, int embeddingDimensions, Exception originalException, CancellationToken cancellationToken)
    {
        try
        {
            // Log the original schema error
            _logger.LogWarning(originalException, "Attempting to recover from PostgreSQL schema error for {IndexName}", indexName);

            // Try to ensure schema compatibility first
            await EnsurePostgresSchemaCompatibilityAsync(indexName, cancellationToken);

            // Then try to create the collection again
            var definition = BuildSkUnifiedDefinition(embeddingDimensions);
            var collection = GetCollection(indexName, definition);
            await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully recovered from schema error for PostgreSQL table {IndexName}", indexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not recover from PostgreSQL schema error for {IndexName}, original error: {OriginalError}", indexName, originalException.Message);
            throw; // Re-throw if we can't recover
        }
    }

    /// <summary>
    /// Gets the PostgreSQL connection string from the current configuration.
    /// </summary>
    private string? GetPostgresConnectionString()
    {
        try
        {
            return _configuration.GetConnectionString("kmvectordb");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve PostgreSQL connection string for schema compatibility checks");
            return null;
        }
    }

    /// <summary>
    /// Gets an effective display filename for UI display, falling back to extracting from full filename if DisplayFileName is null.
    /// This provides backward compatibility with existing records that don't have DisplayFileName populated.
    /// </summary>
    private static string? GetEffectiveDisplayFileName(string? displayFileName, string fileName)
    {
        if (!string.IsNullOrEmpty(displayFileName))
        {
            return displayFileName;
        }

        // Fallback for older records without DisplayFileName: extract just the filename part
        if (!string.IsNullOrEmpty(fileName))
        {
            try
            {
                return Path.GetFileName(fileName);
            }
            catch
            {
                // If Path.GetFileName fails, return the original fileName
                return fileName;
            }
        }

        return null;
    }

    /// <summary>
    /// Normalizes similarity scores across different vector store providers.
    /// PostgreSQL pgvector returns cosine distance (0..2, lower is better),
    /// while Azure AI Search returns similarity scores (0..1, higher is better).
    /// We convert pgvector distance to similarity using: similarity = 1 - distance
    /// </summary>
    private double NormalizeScore(double rawScore)
    {
        if (_options.StoreType == VectorStoreType.PostgreSQL)
        {
            // PostgreSQL pgvector returns cosine distance (0 = identical, 2 = opposite)
            // Convert to similarity score (1 = identical, 0 = opposite): similarity = 1 - distance
            // Clamp to [0, 1] range for safety (distances > 1 are rare but theoretically possible)
            var similarity = Math.Clamp(1.0 - rawScore, 0.0, 1.0);
            _logger.LogDebug("Converted PostgreSQL distance to similarity: {Distance} -> {Similarity}", rawScore, similarity);
            return similarity;
        }

        // AI Search returns similarity score directly (0..1, higher is better)
        return rawScore;
    }

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();
    private static string BuildFileIndexKey(string normIndex, string fileName) => $"{RedisFileIndexKeyPrefix}:{normIndex}:{fileName}";
    private static string BuildDocPartitionsKey(string normIndex, string documentId) => $"{RedisDocPartitionsKeyPrefix}:{normIndex}:{documentId}";
    private static string BuildAppFileIndexKey(string normIndex, string fileName) => $"{AppCacheFileIndexKeyPrefix}:{normIndex}:{fileName}";
    private static string BuildAppDocPartitionsKey(string normIndex, string documentId) => $"{AppCacheDocPartitionsKeyPrefix}:{normIndex}:{documentId}";
}
