using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;

namespace Microsoft.Greenlight.Shared.Services.ContentReference;

/// <summary>
/// SK Vector Store-backed repository for ContentReferenceItem indexing.
/// Uses a dedicated system index per ContentReferenceType.
/// </summary>
public class ContentReferenceSemanticKernelVectorStoreRepository : IContentReferenceVectorRepository
{
    private readonly ILogger<ContentReferenceSemanticKernelVectorStoreRepository> _logger;
    private readonly IAiEmbeddingService _embeddingService;
    private readonly ISemanticKernelVectorStoreProvider _provider;
    private readonly ITextChunkingService _chunker;
    private readonly IOptionsMonitor<ServiceConfigurationOptions> _options;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbFactory;
    private readonly IContentReferenceService _contentReferenceService;

    public ContentReferenceSemanticKernelVectorStoreRepository(
        ILogger<ContentReferenceSemanticKernelVectorStoreRepository> logger,
        IAiEmbeddingService embeddingService,
        ISemanticKernelVectorStoreProvider provider,
        ITextChunkingService chunker,
        IOptionsMonitor<ServiceConfigurationOptions> options,
        IDbContextFactory<DocGenerationDbContext> dbFactory,
        IContentReferenceService contentReferenceService)
    {
        _logger = logger;
        _embeddingService = embeddingService;
        _provider = provider;
        _chunker = chunker;
        _options = options;
        _dbFactory = dbFactory;
        _contentReferenceService = contentReferenceService;
    }

    public async Task IndexAsync(ContentReferenceItem reference, CancellationToken ct = default)
    {
        var indexName = GetIndexName(reference.ReferenceType);
        var (deployment, dims) = await _embeddingService.ResolveEmbeddingConfigForContentReferenceTypeAsync(reference.ReferenceType);

        await _provider.EnsureCollectionAsync(indexName, dims, ct);

        var text = reference.RagText;
        if (string.IsNullOrEmpty(text))
        {
            // Centralized content text resolution (no chunking here)
            text = await _contentReferenceService.GetContentTextForContentReferenceItem(reference);
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation("No content text for reference {RefId} of type {Type}; skipping indexing", reference.Id, reference.ReferenceType);
            return;
        }

        var vsOpts = _options.CurrentValue.GreenlightServices.VectorStore;
        var chunkSize = vsOpts.ChunkSize <= 0 ? 1000 : vsOpts.ChunkSize;
        var chunkOverlap = vsOpts.ChunkOverlap < 0 ? 0 : vsOpts.ChunkOverlap;

        var chunks = _chunker.ChunkText(text, chunkSize, chunkOverlap);
        if (chunks.Count == 0)
        {
            _logger.LogInformation("Chunking produced no chunks for reference {RefId}; skipping", reference.Id);
            return;
        }

        string documentId = BuildDocumentId(reference);
        string fileName = BuildFileName(reference);

        // Resolve FileStorageSourceId for provenance tagging if available via ContentReferenceFileAcknowledgment
        Guid? fileStorageSourceId = null;
        try
        {
            await using var dbProbe = await _dbFactory.CreateDbContextAsync(ct);
            var link = await dbProbe.Set<Microsoft.Greenlight.Shared.Models.FileStorage.ContentReferenceFileAcknowledgment>()
                .Include(j => j.FileAcknowledgmentRecord)
                .FirstOrDefaultAsync(j => j.ContentReferenceItemId == reference.Id, ct);
            fileStorageSourceId = link?.FileAcknowledgmentRecord?.FileStorageSourceId;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve FileStorageSourceId for content reference {RefId}", reference.Id);
        }
        var records = new List<SkVectorChunkRecord>(chunks.Count);
        int partition = 0;
        foreach (var chunk in chunks)
        {
            var embedding = await _embeddingService.GenerateEmbeddingsAsync(chunk, deployment, dims);
            records.Add(new SkVectorChunkRecord
            {
                DocumentId = documentId,
                FileName = fileName,
                DocumentReference = reference.Id.ToString(),
                ChunkText = Truncate(chunk, vsOpts.MaxChunkTextLength),
                Embedding = embedding,
                PartitionNumber = partition++,
                IngestedAt = DateTimeOffset.UtcNow,
                Tags = new Dictionary<string, List<string?>>
                {
                    ["contentReferenceId"] = [ reference.Id.ToString() ],
                    ["referenceType"] = [ reference.ReferenceType.ToString() ],
                    ["sourceId"] = [ reference.ContentReferenceSourceId?.ToString() ],
                    // For ExternalFile provenance, include file hash if available
                    ["fileHash"] = string.IsNullOrWhiteSpace(reference.FileHash) ? new List<string?>() : new List<string?> { reference.FileHash },
                    ["fileStorageSourceId"] = fileStorageSourceId.HasValue ? new List<string?> { fileStorageSourceId.Value.ToString() } : new List<string?>()
                }
            });
        }

        await _provider.UpsertAsync(indexName, records, ct);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Set<ContentReferenceVectorDocument>()
            .FirstOrDefaultAsync(x => x.ContentReferenceItemId == reference.Id && x.VectorStoreIndexName == indexName, ct);
        if (existing == null)
        {
            existing = new ContentReferenceVectorDocument
            {
                ContentReferenceItemId = reference.Id,
                ReferenceType = reference.ReferenceType,
                VectorStoreIndexName = indexName,
                VectorStoreDocumentId = documentId,
                ChunkCount = records.Count,
                IndexedUtc = DateTime.UtcNow,
                IsIndexed = true
            };
            db.Add(existing);
        }
        else
        {
            existing.ReferenceType = reference.ReferenceType;
            existing.VectorStoreDocumentId = documentId;
            existing.ChunkCount = records.Count;
            existing.IndexedUtc = DateTime.UtcNow;
            existing.IsIndexed = true;
            db.Update(existing);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ReindexAllAsync(ContentReferenceType type, CancellationToken ct = default)
    {
        var indexName = GetIndexName(type);

        // Clear the index for this type
        await _provider.ClearCollectionAsync(indexName, ct);

        // Remove tracking rows
        await using (var db = await _dbFactory.CreateDbContextAsync(ct))
        {
            var rows = await db.Set<ContentReferenceVectorDocument>()
                .Where(x => x.VectorStoreIndexName == indexName)
                .ToListAsync(ct);
            if (rows.Count > 0)
            {
                db.RemoveRange(rows);
                await db.SaveChangesAsync(ct);
            }
        }

        // Enumerate content references of this type
        await using var db2 = await _dbFactory.CreateDbContextAsync(ct);
        var refs = await db2.ContentReferenceItems.AsNoTracking()
            .Where(r => r.ReferenceType == type)
            .ToListAsync(ct);

        foreach (var reference in refs)
        {
            try { await IndexAsync(reference, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reindex content reference {RefId} of type {Type}", reference.Id, type);
            }
        }
    }

    public async Task DeleteAsync(Guid referenceId, ContentReferenceType type, CancellationToken ct = default)
    {
        var indexName = GetIndexName(type);
        var fileName = BuildFileName(referenceId, type);
        await _provider.DeleteFileAsync(indexName, fileName, ct);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Set<ContentReferenceVectorDocument>()
            .Where(x => x.ContentReferenceItemId == referenceId && x.VectorStoreIndexName == indexName)
            .ToListAsync(ct);
        if (rows.Count > 0)
        {
            db.RemoveRange(rows);
            await db.SaveChangesAsync(ct);
        }
    }

    private static string GetIndexName(ContentReferenceType type) => type switch
    {
        ContentReferenceType.GeneratedDocument => SystemIndexes.GeneratedDocumentContentReferenceIndex,
        ContentReferenceType.GeneratedSection => SystemIndexes.GeneratedSectionContentReferenceIndex,
        ContentReferenceType.ExternalFile => SystemIndexes.ExternalFileContentReferenceIndex,
        ContentReferenceType.ReviewItem => SystemIndexes.ReviewItemContentReferenceIndex,
        ContentReferenceType.ExternalLinkAsset => SystemIndexes.ExternalLinkAssetContentReferenceIndex,
        _ => SystemIndexes.GeneratedDocumentContentReferenceIndex
    };

    private static string BuildDocumentId(ContentReferenceItem item) => $"cr-{item.Id}";
    private static string BuildFileName(ContentReferenceItem item) => $"{item.ReferenceType}-{item.Id}";
    private static string BuildFileName(Guid id, ContentReferenceType type) => $"{type}-{id}";

    private static string Truncate(string text, int maxLength)
    {
        if (maxLength <= 0 || text.Length <= maxLength) return text;
        return text.Substring(0, Math.Min(text.Length, maxLength)) + "...";
    }
}
