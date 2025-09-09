// Copyright (c) Microsoft Corporation. All rights reserved.
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using AutoMapper.QueryableExtensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Services.Caching;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Services.FileStorage;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <inheritdoc />
    public class ContentReferenceService : IContentReferenceService
    {
        private readonly IAppCache _cache;
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly ILogger<ContentReferenceService> _logger;
        private readonly IContentReferenceGenerationServiceFactory _generationServiceFactory;
        private readonly IAiEmbeddingService _aiEmbeddingService;
        private readonly ISemanticKernelVectorStoreProvider _vectorStoreProvider;
        private readonly IFileUrlResolverService _fileUrlResolver;
        private readonly IOptionsMonitor<ServiceConfigurationOptions> _options;
        private readonly IMapper _mapper;
        private readonly IFileStorageServiceFactory _fileStorageServiceFactory;

        // We continue using the same CacheKey name here.
        private const string CacheKey = "ContentReferences:All";
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);
        private const string AssistantListCacheKey = "ContentReferences:AssistantList:default";
        private readonly TimeSpan _assistantListCacheDuration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Constructor for the (now updated) content reference service.
        /// </summary>
        /// <param name="cache">Centralized cache for storing lightweight reference DTOs</param>
        /// <param name="dbContextFactory">Factory for creating database contexts</param>
        /// <param name="generationServiceFactory">Factory for resolving content reference generation services</param>
        /// <param name="aiEmbeddingService">Service for generating and comparing embeddings</param>
        /// <param name="logger">Logger for service diagnostics</param>
        /// <param name="mapper">AutoMapper instance</param>
        public ContentReferenceService(
            IAppCache cache,
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            IContentReferenceGenerationServiceFactory generationServiceFactory,
            IAiEmbeddingService aiEmbeddingService,
            ILogger<ContentReferenceService> logger, IMapper mapper,
            IFileUrlResolverService fileUrlResolver,
            ISemanticKernelVectorStoreProvider vectorStoreProvider,
            IOptionsMonitor<ServiceConfigurationOptions> options,
            IFileStorageServiceFactory fileStorageServiceFactory)
        {
            _cache = cache;
            _dbContextFactory = dbContextFactory;
            _generationServiceFactory = generationServiceFactory;
            _aiEmbeddingService = aiEmbeddingService;
            _logger = logger;
            _mapper = mapper;
            _fileUrlResolver = fileUrlResolver;
            _vectorStoreProvider = vectorStoreProvider;
            _options = options;
            _fileStorageServiceFactory = fileStorageServiceFactory;
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>?> GetAllReferences()
        {
            // Materialize a capped, recent set to avoid heavy scans without cache
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            const int maxResults = 500;
            return await db.ContentReferenceItems
                .AsNoTracking()
                .Where(r => r.ReferenceType != ContentReferenceType.ExternalFile)
                .OrderByDescending(r => r.ModifiedUtc)
                .ThenByDescending(r => r.CreatedUtc)
                .Take(maxResults)
                .ProjectTo<ContentReferenceItemInfo>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>?> SearchReferencesAsync(string searchTerm)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var query = db.ContentReferenceItems
                .AsNoTracking()
                .Where(r => r.ReferenceType != ContentReferenceType.ExternalFile);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowered = searchTerm.ToLower();
                query = query.Where(r => r.DisplayName != null && r.DisplayName.ToLower().Contains(lowered));
            }

            // Apply a reasonable cap to avoid large result payloads on broad terms
            const int maxResults = 100;

            return await query
                .OrderByDescending(r => r.ModifiedUtc)
                .ThenByDescending(r => r.CreatedUtc)
                .Take(maxResults)
                .ProjectTo<ContentReferenceItemInfo>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>?> SearchSimilarReferencesAsync(string searchTerm, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<ContentReferenceItemInfo>();
            }
            // Use vector store to find candidate IDs, then fetch only those rows
            try
            {
                var vsOpts = _options.CurrentValue.GreenlightServices.VectorStore;
                var minRelevance = vsOpts.MinRelevanceScore <= 0 ? 0.7 : vsOpts.MinRelevanceScore;
                var scores = new Dictionary<Guid, double>();

                foreach (var type in Enum.GetValues<ContentReferenceType>())
                {
                    var indexName = GetIndexName(type);
                    if (string.IsNullOrWhiteSpace(indexName)) { continue; }

                    var (deployment, dims) = await _aiEmbeddingService.ResolveEmbeddingConfigForContentReferenceTypeAsync(type);
                    var queryEmbedding = await _aiEmbeddingService.GenerateEmbeddingsAsync(searchTerm, deployment, dims);
                    var searchTop = Math.Min(Math.Max(10, maxResults * 5), vsOpts.MaxSearchResults > 0 ? vsOpts.MaxSearchResults : 50);
                    var matches = await _vectorStoreProvider.SearchAsync(indexName, queryEmbedding, searchTop, minRelevance);
                    foreach (var match in matches)
                    {
                        if (match?.Record?.Tags == null) { continue; }
                        if (!match.Record.Tags.TryGetValue("contentReferenceId", out var list) || list == null || list.Count == 0) { continue; }
                        if (Guid.TryParse(list[0], out var crId))
                        {
                            if (!scores.TryGetValue(crId, out var existing) || match.Score > existing)
                            {
                                scores[crId] = match.Score;
                            }
                        }
                    }
                }

                var orderedIds = scores
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => kv.Key)
                    .Take(Math.Max(1, maxResults))
                    .ToList();

                if (orderedIds.Count == 0)
                {
                    // Fallback: light text search directly in DB
                    await using var dbNoVec = await _dbContextFactory.CreateDbContextAsync();
                    return await dbNoVec.ContentReferenceItems
                        .AsNoTracking()
                        .Where(r => r.ReferenceType != ContentReferenceType.ExternalFile && r.DisplayName != null && EF.Functions.Like(r.DisplayName, "%" + searchTerm + "%"))
                        .OrderByDescending(r => r.ModifiedUtc)
                        .ThenByDescending(r => r.CreatedUtc)
                        .Take(Math.Max(1, maxResults))
                        .ProjectTo<ContentReferenceItemInfo>(_mapper.ConfigurationProvider)
                        .ToListAsync();
                }

                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var infos = await db.ContentReferenceItems
                    .AsNoTracking()
                    .Where(r => orderedIds.Contains(r.Id))
                    .ProjectTo<ContentReferenceItemInfo>(_mapper.ConfigurationProvider)
                    .ToListAsync();

                // Preserve vector order
                var byId = infos.ToDictionary(x => x.Id);
                var ordered = new List<ContentReferenceItemInfo>(infos.Count);
                foreach (var id in orderedIds)
                {
                    if (byId.TryGetValue(id, out var info)) { ordered.Add(info); }
                }
                return ordered;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Vector search failed in SearchSimilarReferencesAsync; falling back to text search");
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                return await db.ContentReferenceItems
                    .AsNoTracking()
                    .Where(r => r.ReferenceType != ContentReferenceType.ExternalFile && r.DisplayName != null && EF.Functions.Like(r.DisplayName, "%" + searchTerm + "%"))
                    .OrderByDescending(r => r.ModifiedUtc)
                    .ThenByDescending(r => r.CreatedUtc)
                    .Take(Math.Max(1, maxResults))
                    .ProjectTo<ContentReferenceItemInfo>(_mapper.ConfigurationProvider)
                    .ToListAsync();
            }
        }

        private static string GetIndexName(ContentReferenceType type) => type switch
        {
            ContentReferenceType.GeneratedDocument => SystemIndexes.GeneratedDocumentContentReferenceIndex,
            ContentReferenceType.GeneratedSection => SystemIndexes.GeneratedSectionContentReferenceIndex,
            ContentReferenceType.ExternalFile => SystemIndexes.ExternalFileContentReferenceIndex,
            ContentReferenceType.ReviewItem => SystemIndexes.ReviewItemContentReferenceIndex,
            ContentReferenceType.ExternalLinkAsset => SystemIndexes.ExternalLinkAssetContentReferenceIndex,
            _ => string.Empty
        };

        /// <inheritdoc />
        public async Task<ContentReferenceItemInfo?> GetReferenceByIdAsync(Guid id, ContentReferenceType type)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            return await db.ContentReferenceItems
                .AsNoTracking()
                .Where(r => r.Id == id && r.ReferenceType == type && r.ReferenceType != ContentReferenceType.ExternalFile)
                .ProjectTo<ContentReferenceItemInfo>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc />
        public async Task<ContentReferenceItemInfo?> GetBySourceIdAsync(Guid sourceId, ContentReferenceType type)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            return await db.ContentReferenceItems
                .AsNoTracking()
                .Where(r => r.ContentReferenceSourceId == sourceId && r.ReferenceType == type && r.ReferenceType != ContentReferenceType.ExternalFile)
                .OrderByDescending(r => r.ModifiedUtc)
                .ThenByDescending(r => r.CreatedUtc)
                .ProjectTo<ContentReferenceItemInfo>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc />
        public async Task<string?> GetContentTextForContentReferenceItem(ContentReferenceItem reference)
        {
            if (reference.RagText != null) { return reference.RagText; }
            if (reference.ContentReferenceSourceId == null) { return null; }

            try
            {
                string? ragText = null;
                switch (reference.ReferenceType)
                {
                    case ContentReferenceType.GeneratedDocument:
                        ragText = await _generationServiceFactory.GetGenerationService<GeneratedDocument>(reference.ReferenceType)?
                            .GenerateContentTextForRagAsync(reference.ContentReferenceSourceId.Value);
                        break;
                    case ContentReferenceType.GeneratedSection:
                        ragText = await _generationServiceFactory.GetGenerationService<ContentNode>(reference.ReferenceType)?
                            .GenerateContentTextForRagAsync(reference.ContentReferenceSourceId.Value);
                        break;
                    case ContentReferenceType.ExternalFile:
                        ragText = await _generationServiceFactory.GetGenerationService<ExportedDocumentLink>(reference.ReferenceType)?
                            .GenerateContentTextForRagAsync(reference.ContentReferenceSourceId.Value);
                        break;
                    case ContentReferenceType.ExternalLinkAsset:
                        ragText = await _generationServiceFactory.GetGenerationService<Microsoft.Greenlight.Shared.Models.FileStorage.ExternalLinkAsset>(reference.ReferenceType)?
                            .GenerateContentTextForRagAsync(reference.ContentReferenceSourceId.Value);
                        break;
                    case ContentReferenceType.ReviewItem:
                        await using (var db = await _dbContextFactory.CreateDbContextAsync())
                        {
                            var reviewInstance = await db.Set<ReviewInstance>().FirstOrDefaultAsync(r => r.Id == reference.ContentReferenceSourceId.Value);
                            if (reviewInstance?.ExportedLinkId != Guid.Empty)
                            {
                                var exportedDocLink = await db.ExportedDocumentLinks.FindAsync(reviewInstance.ExportedLinkId);
                                if (exportedDocLink != null)
                                {
                                    ragText = await _generationServiceFactory.GetGenerationService<ExportedDocumentLink>(ContentReferenceType.ExternalFile)?
                                        .GenerateContentTextForRagAsync(exportedDocLink.Id);
                                }
                            }
                        }
                        break;
                    default:
                        _logger.LogWarning("No content reference generation service found for type {Type}", reference.ReferenceType);
                        return null;
                }

                if (!string.IsNullOrEmpty(ragText))
                {
                    reference.RagText = ragText;
                }
                return ragText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating content text for RAG for reference {Id} of type {Type}", reference.Id, reference.ReferenceType);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task RefreshReferencesCacheAsync()
        {
            // Maintain existing behavior without expanding caching usage.
            await ScanAndUpdateReferencesAsync();
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var references = await db.ContentReferenceItems
                .AsNoTracking()
                .Where(r => r.ReferenceType != ContentReferenceType.ExternalFile)
                .Select(r => new ContentReferenceItemInfo
                {
                    Id = r.Id,
                    ContentReferenceSourceId = r.ContentReferenceSourceId,
                    DisplayName = r.DisplayName,
                    ReferenceType = r.ReferenceType,
                    CreatedDate = r.CreatedUtc,
                    Description = r.Description,
                    CreatedUtc = r.CreatedUtc,
                    ModifiedUtc = r.ModifiedUtc
                })
                .ToListAsync();
            await _cache.SetAsync(CacheKey, references, _cacheDuration, allowDistributed: false);
            // Also invalidate assistant list cache, since underlying data changed
            await InvalidateAssistantReferenceListCacheAsync();
        }

        /// <inheritdoc />
        public async Task<string?> GetRagTextAsync(Guid id)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            return (await db.ContentReferenceItems.FirstOrDefaultAsync(r => r.Id == id))?.RagText;
        }

        /// <inheritdoc />
        public async Task<ContentReferenceItem> GetOrCreateContentReferenceItemAsync(Guid id, ContentReferenceType type)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var reference = await db.ContentReferenceItems.FirstOrDefaultAsync(r => r.Id == id);

            if (reference == null && (type == ContentReferenceType.GeneratedDocument || type == ContentReferenceType.GeneratedSection))
            {
                reference = await db.ContentReferenceItems.FirstOrDefaultAsync(r => r.ReferenceType == type && r.ContentReferenceSourceId == id);
                if (reference != null) { return reference; }
            }

            var isNew = reference == null;
            if (isNew)
            {
                if (type == ContentReferenceType.ExternalFile)
                {
                    var exportedDocLink = await db.ExportedDocumentLinks.FindAsync(id);
                    if (exportedDocLink != null && !string.IsNullOrEmpty(exportedDocLink.FileHash))
                    {
                        var existingReference = await db.ContentReferenceItems
                            .Where(r => r.ReferenceType == ContentReferenceType.ExternalFile && r.FileHash == exportedDocLink.FileHash && r.Id != id)
                            .FirstOrDefaultAsync();
                        if (existingReference != null)
                        {
                            _logger.LogInformation("Found existing reference with matching file hash {FileHash}. Using {ExistingId} instead of {NewId}", exportedDocLink.FileHash, existingReference.Id, id);
                            return existingReference;
                        }
                        reference = new ContentReferenceItem
                        {
                            Id = id,
                            ReferenceType = type,
                            ContentReferenceSourceId = exportedDocLink.Id,
                            DisplayName = exportedDocLink.FileName,
                            Description = $"Uploaded document: {exportedDocLink.FileName}",
                            FileHash = exportedDocLink.FileHash
                        };
                        await EnsureFileAcknowledgmentForExternalFileAsync(db, reference, exportedDocLink);
                    }
                    else
                    {
                        reference = new ContentReferenceItem { Id = id, ReferenceType = type };
                    }
                }
                else
                {
                    reference = new ContentReferenceItem { Id = id, ReferenceType = type };
                }
                db.ContentReferenceItems.Add(reference);
            }

            if (reference.ReferenceType == ContentReferenceType.ReviewItem && string.IsNullOrEmpty(reference.RagText) && reference.ContentReferenceSourceId != null)
            {
                var reviewInstance = await db.Set<ReviewInstance>().FirstOrDefaultAsync(r => r.Id == reference.ContentReferenceSourceId.Value);
                if (reviewInstance?.ExportedLinkId != Guid.Empty)
                {
                    var exportedDocLink = await db.ExportedDocumentLinks.FindAsync(reviewInstance.ExportedLinkId);
                    if (exportedDocLink != null)
                    {
                        try
                        {
                            await EnsureFileAcknowledgmentForExternalFileAsync(db, reference, exportedDocLink);
                            var fileService = _generationServiceFactory.GetGenerationService<ExportedDocumentLink>(ContentReferenceType.ExternalFile);
                            var ragText = await fileService?.GenerateContentTextForRagAsync(exportedDocLink.Id);
                            if (!string.IsNullOrEmpty(ragText)) { reference.RagText = ragText; }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error generating RAG text for review item (file) {Id}", id);
                        }
                    }
                }
            }
            else if (string.IsNullOrEmpty(reference.RagText) && reference.ContentReferenceSourceId != null)
            {
                try
                {
                    var generatedRagText = await GetContentTextForContentReferenceItem(reference);
                    if (!string.IsNullOrEmpty(generatedRagText)) { reference.RagText = generatedRagText; }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating RAG text for reference {Id}", id);
                }
            }

            await db.SaveChangesAsync();
            return reference;
        }

        private async Task EnsureFileAcknowledgmentForExternalFileAsync(DocGenerationDbContext db, ContentReferenceItem reference, ExportedDocumentLink exportedDocLink)
        {
            var fileSource = await db.FileStorageSources
                .Include(s => s.FileStorageHost)
                .FirstOrDefaultAsync(s => s.ContainerOrPath == exportedDocLink.BlobContainer) ??
                             await db.FileStorageSources.FirstOrDefaultAsync(s => s.IsDefault);
            if (fileSource == null) { return; }

            // Get the FileStorageService for this source
            var fileStorageService = await _fileStorageServiceFactory.GetServiceBySourceIdAsync(fileSource.Id);
            if (fileStorageService == null)
            {
                _logger.LogWarning("FileStorageService not found for source {SourceId}, cannot acknowledge file", fileSource.Id);
                return;
            }

            // Check if we already have an acknowledgment for this file
            var existingAck = await db.FileAcknowledgmentRecords
                .Include(a => a.FileStorageSource)
                .FirstOrDefaultAsync(a => a.FileStorageSourceId == fileSource.Id &&
                                          ((!string.IsNullOrEmpty(exportedDocLink.FileHash) && a.FileHash == exportedDocLink.FileHash) ||
                                           a.FileStorageSourceInternalUrl == exportedDocLink.AbsoluteUrl));
            
            Guid? acknowledgmentId = existingAck?.Id;
            
            if (existingAck == null)
            {
                // Register file discovery (no movement for content references)
                var relativePath = exportedDocLink.FileName;
                try
                {
                    acknowledgmentId = await fileStorageService.RegisterFileDiscoveryAsync(relativePath, exportedDocLink.FileHash);
                    
                    // Reload the acknowledgment record that was created by the service
                    existingAck = await db.FileAcknowledgmentRecords
                        .Include(a => a.FileStorageSource)
                        .FirstOrDefaultAsync(a => a.Id == acknowledgmentId);
                    
                    _logger.LogDebug("Registered discovery for content reference file {FileName} with hash {Hash}", 
                        relativePath, exportedDocLink.FileHash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register discovery for file {FileName} through FileStorageService", relativePath);
                    return;
                }
            }

            var joinExists = await db.Set<Shared.Models.FileStorage.ContentReferenceFileAcknowledgment>()
                .AnyAsync(j => j.ContentReferenceItemId == reference.Id && j.FileAcknowledgmentRecordId == existingAck.Id);
            if (!joinExists)
            {
                db.Add(new Shared.Models.FileStorage.ContentReferenceFileAcknowledgment
                {
                    ContentReferenceItemId = reference.Id,
                    FileAcknowledgmentRecordId = existingAck.Id
                });
                await db.SaveChangesAsync();
            }

            try { _ = await _fileUrlResolver.ResolveUrlAsync(existingAck); }
            catch (Exception ex) { _logger.LogDebug(ex, "ExternalLinkAsset ensure step failed for acknowledgment {AckId}", existingAck.Id); }
        }

        /// <inheritdoc />
        public async Task ScanAndUpdateReferencesAsync(CancellationToken ct = default)
        {
            var validDocumentIds = await ProcessGeneratedDocumentReferencesAsync(ct);
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            await db.SaveChangesAsync(ct);
            await RemoveStaleReferencesAsync(validDocumentIds, ct);
        }

        private async Task<HashSet<Guid>> ProcessGeneratedDocumentReferencesAsync(CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var documents = await db.GeneratedDocuments.ToListAsync(ct);
            var validDocumentIds = documents.Select(d => d.Id).ToHashSet();

            var documentService = _generationServiceFactory.GetGenerationService<GeneratedDocument>(ContentReferenceType.GeneratedDocument);
            if (documentService != null)
            {
                var newInfos = new List<ContentReferenceItemInfo>();
                foreach (var doc in documents)
                {
                    try { newInfos.AddRange(await documentService.GenerateReferencesAsync(doc)); }
                    catch (Exception ex) { _logger.LogError(ex, "Error generating references for document {DocId}", doc.Id); }
                }
                await UpsertReferencesAsync(newInfos, db, ct);
            }
            return validDocumentIds;
        }

        private async Task UpsertReferencesAsync(List<ContentReferenceItemInfo> infos, DocGenerationDbContext db, CancellationToken ct)
        {
            foreach (var info in infos)
            {
                if (!info.ContentReferenceSourceId.HasValue) { continue; }
                var existing = await db.ContentReferenceItems
                    .FirstOrDefaultAsync(r => r.ContentReferenceSourceId == info.ContentReferenceSourceId && r.ReferenceType == info.ReferenceType, ct);
                if (existing == null)
                {
                    var entity = new ContentReferenceItem
                    {
                        Id = info.Id,
                        ReferenceType = info.ReferenceType,
                        ContentReferenceSourceId = info.ContentReferenceSourceId,
                        DisplayName = info.DisplayName,
                        Description = info.Description
                    };
                    db.ContentReferenceItems.Add(entity);
                    await EnsureContentReferenceItemWithRagTextAsync(entity, db, saveChanges: false);
                }
                else
                {
                    existing.DisplayName = info.DisplayName;
                    existing.Description = info.Description;
                    db.ContentReferenceItems.Update(existing);
                    await EnsureContentReferenceItemWithRagTextAsync(existing, db, saveChanges: false);
                }
            }
        }

        private async Task RemoveStaleReferencesAsync(HashSet<Guid> validDocumentIds, CancellationToken ct)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var stale = await db.ContentReferenceItems
                .Where(r => r.ReferenceType == ContentReferenceType.GeneratedDocument &&
                            r.ContentReferenceSourceId.HasValue &&
                            !validDocumentIds.Contains(r.ContentReferenceSourceId.Value))
                .ToListAsync(ct);
            if (stale.Any())
            {
                db.ContentReferenceItems.RemoveRange(stale);
                await db.SaveChangesAsync(ct);
            }
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItem>> GetContentReferenceItemsFromIdsAsync(List<Guid> ids)
        {
            if (ids == null || !ids.Any()) { return new List<ContentReferenceItem>(); }
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var items = await db.ContentReferenceItems.Where(r => ids.Contains(r.Id)).ToListAsync();
            var result = new List<ContentReferenceItem>();
            foreach (var item in items)
            {
                result.Add(await EnsureContentReferenceItemWithRagTextAsync(item, db, saveChanges: true));
            }
            return result;
        }

        /// <inheritdoc />
        public async Task RemoveReferenceAsync(Guid referenceId, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var entity = await db.ContentReferenceItems.FirstOrDefaultAsync(r => r.Id == referenceId, ct);
            if (entity != null)
            {
                db.ContentReferenceItems.Remove(entity);
                await db.SaveChangesAsync(ct);
            }
        }

        private async Task<ContentReferenceItem> EnsureContentReferenceItemWithRagTextAsync(ContentReferenceItem reference, DocGenerationDbContext db, bool saveChanges)
        {
            if (reference == null) { throw new ArgumentNullException(nameof(reference)); }
            if (string.IsNullOrEmpty(reference.RagText))
            {
                try
                {
                    var text = await GetContentTextForContentReferenceItem(reference);
                    if (!string.IsNullOrEmpty(text))
                    {
                        reference.RagText = text;
                        if (saveChanges) { await db.SaveChangesAsync(); }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating RAG text for content reference {Id}", reference.Id);
                }
            }
            return reference;
        }

        private bool IsRateLimitError(Exception ex)
        {
            var msg = ex.ToString();
            return msg.Contains("429") || msg.Contains("Too Many Requests") || msg.Contains("quota exceeded") || msg.Contains("rate limit");
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>> GetAssistantReferenceListAsync(int top = 200, ContentReferenceType[]? types = null, CancellationToken ct = default)
        {
            // Use in-proc cache for a short duration to keep selector snappy without distributed complexity
            return await _cache.GetOrCreateAsync(AssistantListCacheKey, async _ =>
            {
                var selectedTypes = types is { Length: > 0 }
                    ? types
                    : Enum.GetValues<ContentReferenceType>().Where(t => t != ContentReferenceType.ExternalFile).ToArray();

                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
                var list = await db.ContentReferenceItems
                    .AsNoTracking()
                    .Where(r => selectedTypes.Contains(r.ReferenceType))
                    .OrderByDescending(r => r.ModifiedUtc)
                    .ThenByDescending(r => r.CreatedUtc)
                    .Take(Math.Max(1, top))
                    .ProjectTo<ContentReferenceItemInfo>(_mapper.ConfigurationProvider)
                    .ToListAsync(ct);

                return list;
            }, _assistantListCacheDuration, allowDistributed: false, token: ct);
        }

        /// <inheritdoc />
        public async Task InvalidateAssistantReferenceListCacheAsync(CancellationToken ct = default)
        {
            await _cache.RemoveAsync(AssistantListCacheKey, ct);
        }

        /// <inheritdoc />
        public async Task<ContentReferenceItemInfo> CreateExternalLinkAssetReferenceAsync(
            Guid externalLinkAssetId,
            string fileName,
            string? fileHash = null,
            CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            // Check for duplicates if we have a file hash
            if (!string.IsNullOrEmpty(fileHash))
            {
                var duplicate = await FindDuplicateByFileHashAsync(fileHash, ContentReferenceType.ExternalLinkAsset, ct);
                if (duplicate != null)
                {
                    _logger.LogInformation("Found duplicate ExternalLinkAsset with hash {FileHash}, using existing reference {ExistingId}",
                        fileHash, duplicate.Id);
                    return _mapper.Map<ContentReferenceItemInfo>(duplicate);
                }
            }

            // Load the ExternalLinkAsset to ensure it exists
            var externalLinkAsset = await db.Set<Models.FileStorage.ExternalLinkAsset>()
                .Include(ela => ela.FileStorageSource)
                .FirstOrDefaultAsync(ela => ela.Id == externalLinkAssetId, ct);

            if (externalLinkAsset == null)
            {
                throw new InvalidOperationException($"ExternalLinkAsset with ID {externalLinkAssetId} not found");
            }

            // Create the ContentReferenceItem
            var contentReferenceItem = new ContentReferenceItem
            {
                Id = Guid.NewGuid(),
                ReferenceType = ContentReferenceType.ExternalLinkAsset,
                ContentReferenceSourceId = externalLinkAssetId,
                DisplayName = fileName,
                Description = $"Uploaded document: {fileName}",
                FileHash = fileHash
            };

            // Register file discovery if we have a FileStorageSource
            if (externalLinkAsset.FileStorageSourceId.HasValue)
            {
                try
                {
                    var fileStorageService = await _fileStorageServiceFactory.GetServiceBySourceIdAsync(externalLinkAsset.FileStorageSourceId.Value);
                    if (fileStorageService != null)
                    {
                        await fileStorageService.RegisterFileDiscoveryAsync(externalLinkAsset.FileName, fileHash, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register file discovery for ExternalLinkAsset {AssetId}", externalLinkAssetId);
                }
            }

            // Generate RAG text if possible
            try
            {
                var ragText = await GetContentTextForContentReferenceItem(contentReferenceItem);
                if (!string.IsNullOrEmpty(ragText))
                {
                    contentReferenceItem.RagText = ragText;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating RAG text for ExternalLinkAsset reference {Id}", contentReferenceItem.Id);
            }

            db.ContentReferenceItems.Add(contentReferenceItem);
            await db.SaveChangesAsync(ct);

            // Invalidate caches
            await InvalidateAssistantReferenceListCacheAsync(ct);

            return _mapper.Map<ContentReferenceItemInfo>(contentReferenceItem);
        }

        /// <inheritdoc />
        public async Task<ContentReferenceItemInfo> CreateExternalFileReferenceAsync(
            ExportedDocumentLink exportedDocumentLink,
            string fileName,
            CancellationToken ct = default)
        {
            if (exportedDocumentLink == null)
            {
                throw new ArgumentNullException(nameof(exportedDocumentLink));
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            // Check for duplicates based on file hash
            if (!string.IsNullOrEmpty(exportedDocumentLink.FileHash))
            {
                var duplicate = await FindDuplicateByFileHashAsync(exportedDocumentLink.FileHash, ContentReferenceType.ExternalFile, ct);
                if (duplicate != null)
                {
                    _logger.LogInformation("Found duplicate ExternalFile with hash {FileHash}, using existing reference {ExistingId}",
                        exportedDocumentLink.FileHash, duplicate.Id);
                    return _mapper.Map<ContentReferenceItemInfo>(duplicate);
                }
            }

            var contentReferenceItem = new ContentReferenceItem
            {
                Id = Guid.NewGuid(),
                ContentReferenceSourceId = exportedDocumentLink.Id,
                ReferenceType = ContentReferenceType.ExternalFile,
                DisplayName = fileName,
                Description = $"Uploaded document: {fileName}",
                FileHash = exportedDocumentLink.FileHash
            };

            // Ensure FileAcknowledgmentRecord exists for this file
            await EnsureFileAcknowledgmentForExternalFileAsync(db, contentReferenceItem, exportedDocumentLink);

            // Generate RAG text if possible
            try
            {
                var ragText = await GetContentTextForContentReferenceItem(contentReferenceItem);
                if (!string.IsNullOrEmpty(ragText))
                {
                    contentReferenceItem.RagText = ragText;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating RAG text for ExternalFile reference {Id}", contentReferenceItem.Id);
            }

            db.ContentReferenceItems.Add(contentReferenceItem);
            await db.SaveChangesAsync(ct);

            // Invalidate caches
            await InvalidateAssistantReferenceListCacheAsync(ct);

            return _mapper.Map<ContentReferenceItemInfo>(contentReferenceItem);
        }

        /// <inheritdoc />
        public async Task<ContentReferenceItem?> FindDuplicateByFileHashAsync(
            string fileHash,
            ContentReferenceType referenceType,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(fileHash))
            {
                return null;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            if (referenceType == ContentReferenceType.ExternalFile)
            {
                // For ExternalFile, join with ExportedDocumentLinks to check file hash
                var existingReference = await db.ContentReferenceItems
                    .Where(r => r.ReferenceType == ContentReferenceType.ExternalFile)
                    .Join(db.ExportedDocumentLinks,
                        r => r.ContentReferenceSourceId,
                        e => e.Id,
                        (r, e) => new { Reference = r, ExportedDoc = e })
                    .Where(j => j.ExportedDoc.FileHash == fileHash)
                    .Select(j => j.Reference)
                    .FirstOrDefaultAsync(ct);

                return existingReference;
            }
            else if (referenceType == ContentReferenceType.ExternalLinkAsset)
            {
                // For ExternalLinkAsset, check FileHash directly on ContentReferenceItem
                return await db.ContentReferenceItems
                    .Where(r => r.ReferenceType == ContentReferenceType.ExternalLinkAsset && r.FileHash == fileHash)
                    .FirstOrDefaultAsync(ct);
            }
            else
            {
                // For other types, check FileHash on ContentReferenceItem
                return await db.ContentReferenceItems
                    .Where(r => r.ReferenceType == referenceType && r.FileHash == fileHash)
                    .FirstOrDefaultAsync(ct);
            }
        }

        /// <inheritdoc />
        public async Task<ContentReferenceItemInfo> CreateFallbackReferenceAsync(
            string fileName,
            ContentReferenceType referenceType,
            string? errorMessage = null,
            CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var description = string.IsNullOrEmpty(errorMessage)
                ? $"Uploaded document: {fileName} (processing failed)"
                : $"Uploaded document: {fileName} (processing failed: {errorMessage})";

            var contentReferenceItem = new ContentReferenceItem
            {
                Id = Guid.NewGuid(),
                ReferenceType = referenceType,
                DisplayName = fileName,
                Description = description,
                // No ContentReferenceSourceId or FileHash since we couldn't process the file
            };

            db.ContentReferenceItems.Add(contentReferenceItem);
            await db.SaveChangesAsync(ct);

            _logger.LogWarning("Created fallback reference for failed upload: {FileName}, Type: {Type}", fileName, referenceType);

            // Invalidate caches
            await InvalidateAssistantReferenceListCacheAsync(ct);

            return _mapper.Map<ContentReferenceItemInfo>(contentReferenceItem);
        }

        /// <inheritdoc />
        public async Task<ContentReferenceItem> CreateReviewReferenceAsync(
            Guid reviewInstanceId,
            string displayName,
            string? fileHash = null,
            CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            // Check if a ContentReferenceItem already exists for this review
            var existingReference = await db.ContentReferenceItems
                .FirstOrDefaultAsync(r => r.ContentReferenceSourceId == reviewInstanceId && 
                                         r.ReferenceType == ContentReferenceType.ReviewItem, ct);

            if (existingReference != null)
            {
                _logger.LogInformation("ContentReferenceItem already exists for review {ReviewId}, returning existing", reviewInstanceId);
                return existingReference;
            }

            // Create a new ContentReferenceItem for this review
            var contentReference = new ContentReferenceItem
            {
                Id = Guid.NewGuid(),
                ContentReferenceSourceId = reviewInstanceId,
                DisplayName = displayName,
                Description = $"Review Document: {displayName}",
                ReferenceType = ContentReferenceType.ReviewItem,
                FileHash = fileHash
            };

            db.ContentReferenceItems.Add(contentReference);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Created content reference {ContentReferenceId} for review {ReviewInstanceId}",
                contentReference.Id, reviewInstanceId);

            // Generate RAG text if possible (this will look up the ReviewInstance and its associated document)
            try
            {
                var ragText = await GetContentTextForContentReferenceItem(contentReference);
                if (!string.IsNullOrEmpty(ragText))
                {
                    contentReference.RagText = ragText;
                    db.ContentReferenceItems.Update(contentReference);
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating RAG text for review reference {Id}", contentReference.Id);
            }

            // Invalidate caches
            await InvalidateAssistantReferenceListCacheAsync(ct);

            return contentReference;
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItem>> GetReviewContentReferenceItemsAsync(
            Guid reviewInstanceId,
            CancellationToken ct = default)
        {
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

                // Find the content reference item for this review
                var reviewContentReference = await db.ContentReferenceItems
                    .FirstOrDefaultAsync(r => r.ContentReferenceSourceId == reviewInstanceId &&
                                            r.ReferenceType == ContentReferenceType.ReviewItem, ct);

                if (reviewContentReference != null)
                {
                    // Ensure RAG text is populated
                    if (string.IsNullOrEmpty(reviewContentReference.RagText))
                    {
                        try
                        {
                            var ragText = await GetContentTextForContentReferenceItem(reviewContentReference);
                            if (!string.IsNullOrEmpty(ragText))
                            {
                                reviewContentReference.RagText = ragText;
                                db.ContentReferenceItems.Update(reviewContentReference);
                                await db.SaveChangesAsync(ct);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error generating RAG text for review reference {Id}", reviewContentReference.Id);
                        }
                    }

                    return new List<ContentReferenceItem> { reviewContentReference };
                }

                return new List<ContentReferenceItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting content reference items for review {ReviewId}", reviewInstanceId);
                return new List<ContentReferenceItem>();
            }
        }
    }
}
