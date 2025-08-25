using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Services.Caching;
using System.Text;

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

        // We continue using the same CacheKey name here.
        private const string CacheKey = "ContentReferences:All";
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);
        private readonly IMapper _mapper;

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
            ILogger<ContentReferenceService> logger, IMapper mapper)
        {
            _cache = cache;
            _dbContextFactory = dbContextFactory;
            _generationServiceFactory = generationServiceFactory;
            _aiEmbeddingService = aiEmbeddingService;
            _logger = logger;
            _mapper = mapper;
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>?> GetAllCachedReferencesAsync()
        {
            // Use LocalOnly to avoid large values in Redis
            var allReferences = await _cache.GetOrCreateAsync(CacheKey, async ct => await CompileReferencesAsync(), _cacheDuration, allowDistributed: false);
            // Filter out External File references here.
            return allReferences?.Where(r => r.ReferenceType != ContentReferenceType.ExternalFile).ToList();
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>?> SearchCachedReferencesAsync(string searchTerm)
        {
            var allReferences = await GetAllCachedReferencesAsync();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return allReferences;
            }

            return allReferences?
                .Where(r => r.DisplayName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
                .ToList();
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>?> SearchSimilarCachedReferencesAsync(string searchTerm, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<ContentReferenceItemInfo>();

            var allReferences = await GetAllCachedReferencesAsync();
            if (allReferences == null || !allReferences.Any())
                return new List<ContentReferenceItemInfo>();

            try
            {
                // Generate embedding for the search term
                var queryEmbedding = await _aiEmbeddingService.GenerateEmbeddingsAsync(searchTerm);
                var results = new List<(ContentReferenceItemInfo Item, float Score)>();

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var referenceIds = allReferences.Select(r => r.Id).ToList();
                var dbReferences = await dbContext.ContentReferenceItems
                    .Where(r => referenceIds.Contains(r.Id))
                    .Include(r => r.Embeddings)
                    .ToListAsync();

                foreach (var reference in dbReferences)
                {
                    try
                    {
                        if (reference.Embeddings.Any())
                        {
                            var referenceInfo = allReferences.FirstOrDefault(r => r.Id == reference.Id);
                            if (referenceInfo == null) continue;

                            // Use the first embedding for scoring (could average in future)
                            var firstEmbedding = DeserializeEmbeddingVector(reference.Embeddings.First().EmbeddingVector);
                            var score = _aiEmbeddingService.CalculateCosineSimilarity(queryEmbedding, firstEmbedding);
                            results.Add((referenceInfo, score));
                        }
                        else
                        {
                            // Generate embeddings on the fly if there are none
                            var embedding = await GenerateEmbeddingsForReferenceAsync(reference, dbContext);
                            if (embedding is { Length: > 0 })
                            {
                                var referenceInfo = allReferences.FirstOrDefault(r => r.Id == reference.Id);
                                if (referenceInfo == null) continue;
                                var score = _aiEmbeddingService.CalculateCosineSimilarity(queryEmbedding, embedding);
                                results.Add((referenceInfo, score));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error generating embeddings for reference {Id} of type {Type}",
                            reference.Id, reference.ReferenceType);
                    }
                }

                return results
                    .OrderByDescending(x => x.Score)
                    .Take(maxResults)
                    .Select(x => x.Item)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing semantic search for references");
                return await SearchCachedReferencesAsync(searchTerm); // Fall back to text search
            }
        }

        /// <inheritdoc />
        public async Task<ContentReferenceItemInfo?> GetCachedReferenceByIdAsync(Guid id, ContentReferenceType type)
        {
            var allReferences = await GetAllCachedReferencesAsync();
            return allReferences?.FirstOrDefault(r => r.Id == id && r.ReferenceType == type);
        }


        /// <inheritdoc />
        public async Task<string?> GetContentTextForContentReferenceItem(ContentReferenceItem reference)
        {
            if (reference.RagText != null)
            {
                return reference.RagText;
            }

            if (reference.ContentReferenceSourceId == null)
                return null;

            try
            {
                string? ragText = null;
                switch (reference.ReferenceType)
                {
                    case ContentReferenceType.GeneratedDocument:
                        var documentService = _generationServiceFactory.GetGenerationService<GeneratedDocument>(reference.ReferenceType);
                        ragText = await documentService?.GenerateContentTextForRagAsync(reference.ContentReferenceSourceId.Value);
                        break;

                    case ContentReferenceType.GeneratedSection:
                        var sectionService = _generationServiceFactory.GetGenerationService<ContentNode>(reference.ReferenceType);
                        ragText = await sectionService?.GenerateContentTextForRagAsync(reference.ContentReferenceSourceId.Value);
                        break;

                    case ContentReferenceType.ExternalFile:
                        var fileService = _generationServiceFactory.GetGenerationService<ExportedDocumentLink>(reference.ReferenceType);
                        ragText = await fileService?.GenerateContentTextForRagAsync(reference.ContentReferenceSourceId.Value);
                        break;

                    case ContentReferenceType.ReviewItem:
                        // ContentReferenceSourceId is a ReviewInstanceId; get the ExportedLinkId from the ReviewInstance
                        await using (var dbContext = await _dbContextFactory.CreateDbContextAsync())
                        {
                            var reviewInstance = await dbContext.Set<ReviewInstance>().FirstOrDefaultAsync(r => r.Id == reference.ContentReferenceSourceId.Value);
                            if (reviewInstance != null && reviewInstance.ExportedLinkId != Guid.Empty)
                            {
                                var exportedDocLink = await dbContext.ExportedDocumentLinks.FindAsync(reviewInstance.ExportedLinkId);
                                if (exportedDocLink != null)
                                {
                                    var fileService2 = _generationServiceFactory.GetGenerationService<ExportedDocumentLink>(ContentReferenceType.ExternalFile);
                                    ragText = await fileService2?.GenerateContentTextForRagAsync(exportedDocLink.Id);
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
            // Rebuild the lightweight DTO list from the database.
            var references = await CompileReferencesAsync();
            // Store in LocalOnly to avoid large payloads in Redis
            await _cache.SetAsync(CacheKey, references, _cacheDuration, allowDistributed: false);
        }

        /// <inheritdoc />
        public async Task<string?> GetRagTextAsync(Guid id)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var reference = await dbContext.ContentReferenceItems.FirstOrDefaultAsync(r => r.Id == id);
            return reference?.RagText;
        }

        /// <inheritdoc />
        public async Task<ContentReferenceItem> GetOrCreateContentReferenceItemAsync(Guid id, ContentReferenceType type)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            // Always check the database first with better query optimization - include FileHash for ExternalFile type
            var query = dbContext.ContentReferenceItems.AsQueryable();
            if (type == ContentReferenceType.ExternalFile)
            {
                query = query.Include(r => r.Embeddings);
            }

            var reference = await query.FirstOrDefaultAsync(r => r.Id == id);
            bool isNew = reference == null;

            if (isNew)
            {
                // For ExternalFile type, first check if there's already a reference with the same ContentReferenceSourceId
                // This handles the most common case where the same file is uploaded multiple times with different IDs
                if (type == ContentReferenceType.ExternalFile)
                {
                    // Try to find the ExportedDocumentLink that this reference points to
                    var exportedDocLink = await dbContext.ExportedDocumentLinks.FindAsync(id);

                    if (exportedDocLink != null && !string.IsNullOrEmpty(exportedDocLink.FileHash))
                    {
                        // Look for existing references with matching file hash
                        var existingReference = await dbContext.ContentReferenceItems
                            .Include(r => r.Embeddings)
                            .Where(r =>
                                r.ReferenceType == ContentReferenceType.ExternalFile &&
                                r.FileHash == exportedDocLink.FileHash &&
                                r.Id != id)
                            .FirstOrDefaultAsync();

                        if (existingReference != null)
                        {
                            _logger.LogInformation(
                                "Found existing reference with matching file hash {FileHash}. Using existing reference {ExistingId} instead of creating new one {NewId}",
                                exportedDocLink.FileHash, existingReference.Id, id);
                            return existingReference;
                        }

                        // No duplicate found, create new reference with file hash
                        reference = new ContentReferenceItem
                        {
                            Id = id,
                            ReferenceType = type,
                            ContentReferenceSourceId = exportedDocLink.Id,
                            DisplayName = exportedDocLink.FileName,
                            Description = $"Uploaded document: {exportedDocLink.FileName}",
                            FileHash = exportedDocLink.FileHash // Ensure file hash is set
                        };
                    }
                    else
                    {
                        // Create basic reference without file hash
                        reference = new ContentReferenceItem
                        {
                            Id = id,
                            ReferenceType = type
                        };
                    }
                }
                else
                {
                    // For non-ExternalFile types, just create a basic reference
                    reference = new ContentReferenceItem
                    {
                        Id = id,
                        ReferenceType = type
                    };
                }

                dbContext.ContentReferenceItems.Add(reference);
            }

            // --- Custom logic for ReviewItem: treat as ExternalFile for RAG/embedding if needed ---
            if (reference.ReferenceType == ContentReferenceType.ReviewItem && string.IsNullOrEmpty(reference.RagText) && reference.ContentReferenceSourceId != null)
            {
                // ContentReferenceSourceId is a ReviewInstanceId; get the ExportedLinkId from the ReviewInstance
                var reviewInstance = await dbContext.Set<ReviewInstance>().FirstOrDefaultAsync(r => r.Id == reference.ContentReferenceSourceId.Value);
                if (reviewInstance != null && reviewInstance.ExportedLinkId != Guid.Empty)
                {
                    var exportedDocLink = await dbContext.ExportedDocumentLinks.FindAsync(reviewInstance.ExportedLinkId);
                    if (exportedDocLink != null)
                    {
                        try
                        {
                            // Use the ExternalFile logic for RAG text generation
                            var fileService = _generationServiceFactory.GetGenerationService<ExportedDocumentLink>(ContentReferenceType.ExternalFile);
                            var ragText = await fileService?.GenerateContentTextForRagAsync(exportedDocLink.Id);
                            if (!string.IsNullOrEmpty(ragText))
                            {
                                reference.RagText = ragText;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error generating RAG text for review item (file) {Id}", id);
                        }
                    }
                }
            }
            // --- End custom logic ---
            else if (string.IsNullOrEmpty(reference.RagText) && reference.ContentReferenceSourceId != null)
            {
                try
                {
                    var generatedRagText = await GetContentTextForContentReferenceItem(reference);
                    if (!string.IsNullOrEmpty(generatedRagText))
                    {
                        reference.RagText = generatedRagText;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating RAG text for reference {Id}", id);
                }
            }

            await dbContext.SaveChangesAsync();
            return reference;
        }

        /// <inheritdoc />
        /// <inheritdoc />
        public async Task ScanAndUpdateReferencesAsync(CancellationToken ct = default)
        {
            // Process each reference type
            var validDocumentIds = await ProcessGeneratedDocumentReferencesAsync(ct);

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            await dbContext.SaveChangesAsync(ct);

            // Remove stale references for all processed types
            await RemoveStaleReferencesAsync(validDocumentIds, ct);

            // Refresh the cache since the set of references has changed.
            await RefreshReferencesCacheAsync();
        }

        /// <summary>
        /// Processes Content References for generated documents
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>HashSet of valid document IDs</returns>
        private async Task<HashSet<Guid>> ProcessGeneratedDocumentReferencesAsync(CancellationToken ct = default)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            // Get all documents
            var documents = await dbContext.GeneratedDocuments.ToListAsync(ct);
            var validDocumentIds = documents.Select(doc => doc.Id).ToHashSet();

            var documentService = _generationServiceFactory.GetGenerationService<GeneratedDocument>(ContentReferenceType.GeneratedDocument);
            if (documentService != null)
            {
                // Generate new reference items for each document
                var newReferenceInfos = new List<ContentReferenceItemInfo>();
                foreach (var doc in documents)
                {
                    try
                    {
                        // The service should return references with ContentReferenceSourceId set to the document's id.
                        var refs = await documentService.GenerateReferencesAsync(doc);
                        newReferenceInfos.AddRange(refs);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error generating references for document {DocumentId}", doc.Id);
                    }
                }

                // Insert or update each new reference (by checking ContentReferenceSourceId)
                await UpsertReferencesAsync(newReferenceInfos, dbContext, ct);
            }

            return validDocumentIds;
        }

        /// <summary>
        /// Inserts or updates references based on ContentReferenceSourceId
        /// </summary>
        /// <param name="referenceInfos">References to upsert</param>
        /// <param name="dbContext">Database context</param>
        /// <param name="ct">Cancellation token</param>
        private async Task UpsertReferencesAsync(List<ContentReferenceItemInfo> referenceInfos, DocGenerationDbContext dbContext, CancellationToken ct = default)
        {
            foreach (var newRef in referenceInfos)
            {
                if (newRef.ContentReferenceSourceId.HasValue)
                {
                    var existingReference = await dbContext.ContentReferenceItems
                        .FirstOrDefaultAsync(r => r.ContentReferenceSourceId == newRef.ContentReferenceSourceId
                                                 && r.ReferenceType == newRef.ReferenceType, ct);
                    if (existingReference == null)
                    {
                        // Create new reference
                        var reference = new ContentReferenceItem
                        {
                            Id = newRef.Id,
                            ReferenceType = newRef.ReferenceType,
                            ContentReferenceSourceId = newRef.ContentReferenceSourceId,
                            DisplayName = newRef.DisplayName,
                            Description = newRef.Description
                        };
                        dbContext.ContentReferenceItems.Add(reference);
                        await EnsureContentReferenceItemWithRagTextAsync(reference, dbContext, saveChanges: false);
                    }
                    else
                    {
                        // Update existing reference
                        existingReference.DisplayName = newRef.DisplayName;
                        existingReference.Description = newRef.Description;
                        dbContext.ContentReferenceItems.Update(existingReference);
                        await EnsureContentReferenceItemWithRagTextAsync(existingReference, dbContext, saveChanges: false);
                    }
                }
            }
        }

        /// <summary>
        /// Removes stale references that no longer have a corresponding source entity
        /// </summary>
        /// <param name="validDocumentIds">Valid document IDs</param>
        /// <param name="ct">Cancellation token</param>
        private async Task RemoveStaleReferencesAsync(HashSet<Guid> validDocumentIds, CancellationToken ct = default)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            // For document references
            var staleDocumentReferences = await dbContext.ContentReferenceItems
                .Where(r => r.ReferenceType == ContentReferenceType.GeneratedDocument &&
                            r.ContentReferenceSourceId.HasValue &&
                            !validDocumentIds.Contains(r.ContentReferenceSourceId.Value))
                .ToListAsync(ct);

            // We don't process external file references - they are handled differently.

            var allStaleReferences = staleDocumentReferences.ToList();

            if (allStaleReferences.Any())
            {

                // Find ContentEmbeddings for each stale reference and remove them first
                foreach (var reference in allStaleReferences)
                {
                    var embeddings = await dbContext.ContentEmbeddings
                        .Where(e => e.ContentReferenceItemId == reference.Id)
                        .ToListAsync(ct);
                    if (embeddings.Any())
                    {
                        dbContext.ContentEmbeddings.RemoveRange(embeddings);
                    }
                }

                dbContext.ContentReferenceItems.RemoveRange(allStaleReferences);
                await dbContext.SaveChangesAsync(ct);
            }
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItem>> GetContentReferenceItemsFromIdsAsync(List<Guid> ids)
        {
            if (ids == null || !ids.Any())
                return new List<ContentReferenceItem>();

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            // Fetch all items from the database
            var existingItems = await dbContext.ContentReferenceItems
                .Where(r => ids.Contains(r.Id))
                .ToListAsync();

            // Identify missing IDs
            var missingIds = ids.Except(existingItems.Select(i => i.Id)).ToList();

            // Create new items for missing IDs
            foreach (var id in missingIds)
            {
                var newItem = new ContentReferenceItem
                {
                    Id = id,
                    ReferenceType = ContentReferenceType.GeneratedDocument // default type
                };
                dbContext.ContentReferenceItems.Add(newItem);
                existingItems.Add(newItem);
            }

            if (missingIds.Any())
                await dbContext.SaveChangesAsync();

            // Ensure RagText is populated for all items
            var result = new List<ContentReferenceItem>();
            foreach (var item in existingItems)
            {
                var ensuredItem = await EnsureContentReferenceItemWithRagTextAsync(item, dbContext, saveChanges: true);
                result.Add(ensuredItem);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task RemoveReferenceAsync(Guid referenceId, CancellationToken ct = default)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            // Remove the content reference item and its associated embeddings from the database
            var referenceItem = await dbContext.ContentReferenceItems
                .Include(r => r.Embeddings)
                .FirstOrDefaultAsync(r => r.Id == referenceId, ct);

            if (referenceItem != null)
            {
                if (referenceItem.Embeddings.Any())
                {
                    dbContext.ContentEmbeddings.RemoveRange(referenceItem.Embeddings);
                }
                dbContext.ContentReferenceItems.Remove(referenceItem);
            }

            await dbContext.SaveChangesAsync(ct);
            // Refresh the cache since the set of references has changed.
            await RefreshReferencesCacheAsync();
        }

        /// <inheritdoc />
        private async Task<ContentReferenceItem> EnsureContentReferenceItemWithRagTextAsync(ContentReferenceItem reference, DocGenerationDbContext dbContext, bool saveChanges = false)
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            if (string.IsNullOrEmpty(reference.RagText))
            {
                try
                {
                    var generatedRagText = await GetContentTextForContentReferenceItem(reference);
                    if (!string.IsNullOrEmpty(generatedRagText))
                    {
                        reference.RagText = generatedRagText;
                        if (saveChanges)
                        {
                            await dbContext.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating RAG text for content reference {Id}", reference.Id);
                }
            }

            return reference;
        }



        private async Task<float[]?> GenerateEmbeddingsForReferenceAsync(ContentReferenceItem reference, DocGenerationDbContext dbContext)
        {
            if (reference?.ContentReferenceSourceId == null)
                return null;

            try
            {
                switch (reference.ReferenceType)
                {
                    case ContentReferenceType.GeneratedDocument:
                        var documentService = _generationServiceFactory.GetGenerationService<GeneratedDocument>(reference.ReferenceType);
                        return await documentService?.GenerateEmbeddingsAsync(reference.ContentReferenceSourceId.Value);

                    case ContentReferenceType.GeneratedSection:
                        var sectionService = _generationServiceFactory.GetGenerationService<ContentNode>(reference.ReferenceType);
                        return await sectionService?.GenerateEmbeddingsAsync(reference.ContentReferenceSourceId.Value);

                    case ContentReferenceType.ExternalFile:
                        var fileService = _generationServiceFactory.GetGenerationService<ExportedDocumentLink>(reference.ReferenceType);
                        return await fileService?.GenerateEmbeddingsAsync(reference.ContentReferenceSourceId.Value);

                    case ContentReferenceType.ReviewItem:
                        // ContentReferenceSourceId is a ReviewInstanceId; get the ExportedLinkId from the ReviewInstance
                        var reviewInstance = await dbContext.ReviewInstances.FirstOrDefaultAsync(r => r.Id == reference.ContentReferenceSourceId.Value);
                        if (reviewInstance != null && reviewInstance.ExportedLinkId != Guid.Empty)
                        {
                            var exportedDocLink = await dbContext.ExportedDocumentLinks.FindAsync(reviewInstance.ExportedLinkId);
                            if (exportedDocLink != null)
                            {
                                var fileServiceForReviewFiles = _generationServiceFactory.GetGenerationService<ExportedDocumentLink>(ContentReferenceType.ExternalFile);
                                return await fileServiceForReviewFiles?.GenerateEmbeddingsAsync(exportedDocLink.Id);
                            }
                        }
                        return null;

                    default:
                        _logger.LogWarning("No content reference generation service found for type {Type}", reference.ReferenceType);
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embeddings for reference {Id} of type {Type}", reference.Id, reference.ReferenceType);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<float[]> GenerateEmbeddingsForQueryAsync(string query)
        {
            return await _aiEmbeddingService.GenerateEmbeddingsAsync(query);
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, float[]>> GenerateEmbeddingsForChunksAsync(List<string> chunks)
        {
            var embeddings = new Dictionary<string, float[]>();

            foreach (var chunk in chunks)
            {
                try
                {
                    var embedding = await _aiEmbeddingService.GenerateEmbeddingsAsync(chunk);
                    if (embedding != null)
                    {
                        embeddings[chunk] = embedding;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating embeddings for chunk");
                }
            }

            return embeddings;
        }

        /// <inheritdoc />
        public List<(string Chunk, float Score)> CalculateSimilarityScores(float[] queryEmbedding, Dictionary<string, float[]> chunkEmbeddings)
        {
            var scores = new List<(string Chunk, float Score)>();

            foreach (var chunkEmbedding in chunkEmbeddings)
            {
                var score = _aiEmbeddingService.CalculateCosineSimilarity(queryEmbedding, chunkEmbedding.Value);
                scores.Add((chunkEmbedding.Key, score));
            }

            return scores.OrderByDescending(x => x.Score).ToList();
        }

        /// <inheritdoc />
        public List<string> SelectTopChunks(List<(string Chunk, float Score)> similarityScores, int topN)
        {
            return similarityScores.Take(topN).Select(x => x.Chunk).ToList();
        }

        /// <inheritdoc />
        public List<string> ChunkContent(string content, int maxTokens)
        {
            if (string.IsNullOrEmpty(content))
                return new List<string>();

            var chunks = new List<string>();
            var words = content.Split(' ');
            var currentChunk = new StringBuilder();
            var currentTokens = 0;
            var wordsPerToken = 0.75; // approximate ratio

            foreach (var word in words)
            {
                int wordTokens = (int)Math.Ceiling(word.Length * 0.25);
                if (currentTokens + wordTokens > maxTokens && currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                    currentTokens = 0;
                }
                currentChunk.Append(word).Append(' ');
                currentTokens += wordTokens;
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return chunks;
        }

        /// <inheritdoc />
        public async Task<Dictionary<(Guid ReferenceId, string Chunk), float[]>> GetOrCreateEmbeddingsForContentAsync(
            List<ContentReferenceItem> references,
            int maxChunkTokens = 1200)
        {
            var result = new Dictionary<(Guid ReferenceId, string Chunk), float[]>();

            if (!references.Any())
                return result;

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            foreach (var reference in references.Where(r => !string.IsNullOrEmpty(r.RagText)))
            {
                var storedEmbeddings = await dbContext.ContentEmbeddings
                    .Where(e => e.ContentReferenceItemId == reference.Id)
                    .OrderBy(e => e.SequenceNumber)
                    .ToListAsync();

                if (reference.RagText != null && (!storedEmbeddings.Any() ||
                    (storedEmbeddings.Any() &&
                     !AreChunksUpToDate(storedEmbeddings.Select(e => e.ChunkText).ToList(), reference.RagText, maxChunkTokens))))
                {
                    await GenerateAndStoreEmbeddingsAsync(reference, dbContext, maxChunkTokens);
                    storedEmbeddings = await dbContext.ContentEmbeddings
                        .Where(e => e.ContentReferenceItemId == reference.Id)
                        .OrderBy(e => e.SequenceNumber)
                        .ToListAsync();
                }

                foreach (var embedding in storedEmbeddings)
                {
                    var vector = DeserializeEmbeddingVector(embedding.EmbeddingVector);
                    if (vector != null && vector.Length > 0)
                    {
                        var key = (reference.Id, embedding.ChunkText);
                        string prefixedChunk = $"Reference ID: {reference.Id}{(reference.DisplayName != null ? $" - {reference.DisplayName}" : "")}:\n{embedding.ChunkText}";
                        result.Add((reference.Id, prefixedChunk), vector);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Generates and stores embeddings for a content reference item with adaptive parallelism to handle rate limits.
        /// </summary>
        private async Task GenerateAndStoreEmbeddingsAsync(ContentReferenceItem reference, DocGenerationDbContext dbContext, int maxChunkTokens)
        {
            if (string.IsNullOrEmpty(reference.RagText))
                return;

            var existingEmbeddings = await dbContext.ContentEmbeddings
                .Where(e => e.ContentReferenceItemId == reference.Id)
                .ToListAsync();

            if (existingEmbeddings.Any())
            {
                dbContext.ContentEmbeddings.RemoveRange(existingEmbeddings);
                await dbContext.SaveChangesAsync();
            }

            var chunks = ChunkContent(reference.RagText, maxChunkTokens);
            var pendingChunks = new List<(int Index, string Chunk)>();

            for (int i = 0; i < chunks.Count; i++)
            {
                pendingChunks.Add((i, chunks[i]));
            }

            // Initial parallel processing settings
            int maxParallelism = 100;
            int currentParallelism = 20;
            int minParallelism = 1;
            TimeSpan baseDelay = TimeSpan.FromMilliseconds(200);
            int consecutiveSuccesses = 0;
            int consecutiveFailures = 0;

            // Process chunks until done
            while (pendingChunks.Count > 0)
            {
                var batch = pendingChunks.Take(currentParallelism).ToList();
                pendingChunks.RemoveRange(0, Math.Min(batch.Count, pendingChunks.Count));

                var tasks = batch.Select(async item =>
                {
                    try
                    {
                        var embeddingVector = await _aiEmbeddingService.GenerateEmbeddingsAsync(item.Chunk);
                        return (Index: item.Index, Chunk: item.Chunk, Vector: embeddingVector, Error: (Exception)null);
                    }
                    catch (Exception ex)
                    {
                        return (Index: item.Index, Chunk: item.Chunk, Vector: (float[])null, Error: ex);
                    }
                }).ToList();

                var results = await Task.WhenAll(tasks);
                bool hadRateLimitError = false;
                var successfulEmbeddings = new List<(int Index, string Chunk, float[] Vector)>();
                var failedChunks = new List<(int Index, string Chunk)>();

                foreach (var result in results)
                {
                    if (result.Error != null)
                    {
                        // Check if this is a rate limit error
                        if (IsRateLimitError(result.Error))
                        {
                            _logger.LogWarning("Rate limit exceeded when generating embedding for chunk {ChunkIndex}. Current parallelism: {Parallelism}",
                                result.Index, currentParallelism);
                            hadRateLimitError = true;
                            failedChunks.Add((result.Index, result.Chunk));
                        }
                        else
                        {
                            _logger.LogError(result.Error, "Error generating embedding for chunk {ChunkIndex} of reference {ReferenceId}",
                                result.Index, reference.Id);
                            failedChunks.Add((result.Index, result.Chunk));
                        }
                    }
                    else if (result.Vector != null && result.Vector.Length > 0)
                    {
                        successfulEmbeddings.Add((result.Index, result.Chunk, result.Vector));
                    }
                }

                // Add successful embeddings to database
                foreach (var success in successfulEmbeddings)
                {
                    var embedding = new ContentEmbedding
                    {
                        ContentReferenceItemId = reference.Id,
                        ChunkText = success.Chunk,
                        EmbeddingVector = SerializeEmbeddingVector(success.Vector),
                        SequenceNumber = success.Index,
                        GeneratedUtc = DateTime.UtcNow
                    };
                    dbContext.ContentEmbeddings.Add(embedding);
                }

                // Save completed embeddings
                if (successfulEmbeddings.Any())
                {
                    await dbContext.SaveChangesAsync();
                }

                // Re-queue failed chunks
                pendingChunks.AddRange(failedChunks);

                // Adjust parallelism based on errors
                if (hadRateLimitError)
                {
                    consecutiveFailures++;
                    consecutiveSuccesses = 0;

                    // Reduce parallelism more aggressively if we keep hitting rate limits
                    int reductionFactor = Math.Max(2, consecutiveFailures);
                    currentParallelism = Math.Max(minParallelism, currentParallelism / reductionFactor);

                    _logger.LogInformation("Reduced parallelism to {Parallelism} tasks due to rate limits", currentParallelism);

                    // Add exponential backoff delay
                    var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, consecutiveFailures - 1));
                    _logger.LogInformation("Backing off for {DelayMs}ms before retrying", delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }
                else if (successfulEmbeddings.Count == batch.Count) // All succeeded
                {
                    consecutiveSuccesses++;
                    consecutiveFailures = 0;

                    // Gradually increase parallelism on continued success
                    if (consecutiveSuccesses >= 2 && currentParallelism < maxParallelism)
                    {
                        currentParallelism = Math.Min(maxParallelism, currentParallelism + 1);
                        _logger.LogInformation("Increased parallelism to {Parallelism} tasks", currentParallelism);
                    }
                }
            }
        }

        private bool IsRateLimitError(Exception ex)
        {
            string exceptionMessage = ex.ToString();
            return exceptionMessage.Contains("429") ||
                   exceptionMessage.Contains("Too Many Requests") ||
                   exceptionMessage.Contains("quota exceeded") ||
                   exceptionMessage.Contains("rate limit");
        }

        private bool AreChunksUpToDate(List<string> storedChunks, string currentText, int maxChunkTokens)
        {
            var newChunks = ChunkContent(currentText, maxChunkTokens);
            if (storedChunks.Count != newChunks.Count)
                return false;
            for (int i = 0; i < storedChunks.Count; i++)
            {
                if (storedChunks[i] != newChunks[i])
                    return false;
            }
            return true;
        }

        private byte[] SerializeEmbeddingVector(float[] vector)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(vector.Length);
            foreach (var value in vector)
            {
                binaryWriter.Write(value);
            }
            return memoryStream.ToArray();
        }

        private float[] DeserializeEmbeddingVector(byte[] bytes)
        {
            using var memoryStream = new MemoryStream(bytes);
            using var binaryReader = new BinaryReader(memoryStream);
            int length = binaryReader.ReadInt32();
            var vector = new float[length];
            for (int i = 0; i < length; i++)
            {
                vector[i] = binaryReader.ReadSingle();
            }
            return vector;
        }

        private async Task<List<ContentReferenceItemInfo>> CompileReferencesAsync()
        {
            var references = new List<ContentReferenceItemInfo>();
            try
            {
                await AddGeneratedDocumentReferencesAsync(references);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling content references");
            }
            return references;
        }

        private async Task AddGeneratedDocumentReferencesAsync(List<ContentReferenceItemInfo> references)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var documents = await dbContext.GeneratedDocuments
                    .AsNoTracking()
                    .ToListAsync();

                if (!documents.Any())
                    return;

                var documentService = _generationServiceFactory.GetGenerationService<GeneratedDocument>(ContentReferenceType.GeneratedDocument);
                if (documentService == null)
                {
                    _logger.LogError("No content reference generation service found for type {Type}", ContentReferenceType.GeneratedDocument);
                    references.AddRange(documents.Select(doc => new ContentReferenceItemInfo
                    {
                        Id = Guid.NewGuid(),
                        ContentReferenceSourceId = doc.Id,
                        DisplayName = doc.Title,
                        ReferenceType = ContentReferenceType.GeneratedDocument,
                        CreatedDate = doc.CreatedUtc,
                        Description = $"Document: {doc.DocumentProcess}"
                    }));
                    return;
                }

                foreach (var document in documents)
                {
                    try
                    {
                        var documentReferences = await documentService.GenerateReferencesAsync(document);
                        references.AddRange(documentReferences);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error generating references for document {DocumentId}", document.Id);
                        references.Add(new ContentReferenceItemInfo
                        {
                            Id = Guid.NewGuid(),
                            ContentReferenceSourceId = document.Id,
                            DisplayName = document.Title,
                            ReferenceType = ContentReferenceType.GeneratedDocument,
                            CreatedDate = document.CreatedUtc,
                            Description = $"Document: {document.DocumentProcess}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading document references");
            }
        }
    }
}
