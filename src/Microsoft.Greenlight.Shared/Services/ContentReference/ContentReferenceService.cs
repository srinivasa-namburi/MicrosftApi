using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using System.Text;
using System.Text.Json;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <inheritdoc />
    public class ContentReferenceService : IContentReferenceService
    {
        private readonly IDistributedCache _cache;
        private readonly DocGenerationDbContext _dbContext;
        private readonly ILogger<ContentReferenceService> _logger;
        private readonly IContentReferenceGenerationServiceFactory _generationServiceFactory;
        private readonly IAiEmbeddingService _aiEmbeddingService;

        // We continue using the same CacheKey name here.
        private const string CacheKey = "AllContentReferences";
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);
        private readonly IMapper _mapper;

        /// <summary>
        /// Constructor for the (now updated) content reference service.
        /// </summary>
        /// <param name="cache">Distributed cache for storing lightweight reference DTOs</param>
        /// <param name="dbContext">Database context for accessing document data</param>
        /// <param name="generationServiceFactory">Factory for resolving content reference generation services</param>
        /// <param name="aiEmbeddingService">Service for generating and comparing embeddings</param>
        /// <param name="logger">Logger for service diagnostics</param>
        /// <param name="mapper">AutoMapper instance</param>
        public ContentReferenceService(
            IDistributedCache cache,
            DocGenerationDbContext dbContext,
            IContentReferenceGenerationServiceFactory generationServiceFactory,
            IAiEmbeddingService aiEmbeddingService,
            ILogger<ContentReferenceService> logger, IMapper mapper)
        {
            _cache = cache;
            _dbContext = dbContext;
            _generationServiceFactory = generationServiceFactory;
            _aiEmbeddingService = aiEmbeddingService;
            _logger = logger;
            _mapper = mapper;
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>?> GetAllCachedReferencesAsync()
        {
            var cachedData = await _cache.GetStringAsync(CacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<ContentReferenceItemInfo>>(cachedData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing references from cache");
                }
            }

            // When cache is missing or deserialization fails, rebuild from database.
            var references = await CompileReferencesAsync();
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheDuration
            };

            await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(references), cacheOptions);
            return references;
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>?> SearchCachedReferencesAsync(string searchTerm)
        {
            var allReferences = await GetAllCachedReferencesAsync();
            if (string.IsNullOrWhiteSpace(searchTerm))
                return allReferences;

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

                // Use the database to get stored embeddings (or generate if needed)
                var referenceIds = allReferences.Select(r => r.Id).ToList();
                var dbReferences = await _dbContext.ContentReferenceItems
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
                            var embedding = await GenerateEmbeddingsForReferenceAsync(reference);
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
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheDuration
            };
            await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(references), cacheOptions);
        }

        /// <inheritdoc />
        public async Task<string?> GetRagTextAsync(Guid id)
        {
            var reference = await _dbContext.ContentReferenceItems.FirstOrDefaultAsync(r => r.Id == id);
            return reference?.RagText;
        }

        /// <inheritdoc />
        /// <inheritdoc />
        public async Task<ContentReferenceItem> GetOrCreateContentReferenceItemAsync(Guid id, ContentReferenceType type)
        {
            // Always check the database first
            var reference = await _dbContext.ContentReferenceItems.FirstOrDefaultAsync(r => r.Id == id);
            bool isNew = reference == null;

            // Try to get additional info from the lightweight DTO cache
            var cacheReference = await GetCachedReferenceByIdAsync(id, type);

            if (isNew)
            {
                // Prevent duplicate references for the same content source:
                if (cacheReference?.ContentReferenceSourceId != null)
                {
                    var existingReference = await _dbContext.ContentReferenceItems
                        .FirstOrDefaultAsync(r => r.ContentReferenceSourceId == cacheReference.ContentReferenceSourceId && r.ReferenceType == type);
                    if (existingReference != null)
                    {
                        _logger.LogInformation("Found existing reference for content source {SourceId}", cacheReference.ContentReferenceSourceId);
                        return existingReference;
                    }
                }

                reference = _mapper.Map<ContentReferenceItem>(cacheReference);
                reference.ReferenceType = type;
                
                _dbContext.ContentReferenceItems.Add(reference);
            }
            else
            {
                if (cacheReference != null)
                {
                    if (string.IsNullOrEmpty(reference.DisplayName) && !string.IsNullOrEmpty(cacheReference.DisplayName))
                        reference.DisplayName = cacheReference.DisplayName;
                    if (string.IsNullOrEmpty(reference.Description) && !string.IsNullOrEmpty(cacheReference.Description))
                        reference.Description = cacheReference.Description;
                    if (reference.ContentReferenceSourceId == null && cacheReference.ContentReferenceSourceId != null)
                        reference.ContentReferenceSourceId = cacheReference.ContentReferenceSourceId;
                }
            }

            // If needed, generate RAG text.
            if (string.IsNullOrEmpty(reference.RagText) && (reference.ContentReferenceSourceId != null || cacheReference?.ContentReferenceSourceId != null))
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

            if (!isNew)
            {
                _dbContext.ContentReferenceItems.Update(reference);
            }

            await _dbContext.SaveChangesAsync();
            return reference;
        }


        /// <inheritdoc />
        /// <inheritdoc />
        public async Task ScanAndUpdateReferencesAsync(CancellationToken ct = default)
        {
            // Handle GeneratedDocument references
            var documents = await _dbContext.GeneratedDocuments.ToListAsync(ct);
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
                foreach (var newRef in newReferenceInfos)
                {
                    if (newRef.ContentReferenceSourceId.HasValue)
                    {
                        var existingReference = await _dbContext.ContentReferenceItems
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
                            _dbContext.ContentReferenceItems.Add(reference);
                            await EnsureContentReferenceItemWithRagTextAsync(reference, saveChanges: false);
                        }
                        else
                        {
                            // Update existing reference
                            existingReference.DisplayName = newRef.DisplayName;
                            existingReference.Description = newRef.Description;
                            _dbContext.ContentReferenceItems.Update(existingReference);
                            await EnsureContentReferenceItemWithRagTextAsync(existingReference, saveChanges: false);
                        }
                    }
                }
            }

            await _dbContext.SaveChangesAsync(ct);

            // Remove stale references: find references of type GeneratedDocument whose ContentReferenceSourceId no longer exists.
            var staleReferences = await _dbContext.ContentReferenceItems
                .Where(r => r.ReferenceType == ContentReferenceType.GeneratedDocument &&
                            r.ContentReferenceSourceId.HasValue &&
                            !validDocumentIds.Contains(r.ContentReferenceSourceId.Value))
                .ToListAsync(ct);

            if (staleReferences.Any())
            {
                _dbContext.ContentReferenceItems.RemoveRange(staleReferences);
                await _dbContext.SaveChangesAsync(ct);
            }

            // Refresh the cache since the set of references has changed.
            await RefreshReferencesCacheAsync();
        }


        /// <inheritdoc />
        /// <inheritdoc />
        public async Task<List<ContentReferenceItem>> GetContentReferenceItemsFromIdsAsync(List<Guid> ids)
        {
            if (ids == null || !ids.Any())
                return new List<ContentReferenceItem>();

            // Fetch all items from the database
            var existingItems = await _dbContext.ContentReferenceItems
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
                _dbContext.ContentReferenceItems.Add(newItem);
                existingItems.Add(newItem);
            }

            if (missingIds.Any())
                await _dbContext.SaveChangesAsync();

            // Ensure RagText is populated for all items
            var result = new List<ContentReferenceItem>();
            foreach (var item in existingItems)
            {
                var ensuredItem = await EnsureContentReferenceItemWithRagTextAsync(item);
                result.Add(ensuredItem);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task RemoveReferenceAsync(Guid referenceId, CancellationToken ct = default)
        {
            // Remove the content reference item and its associated embeddings from the database
            var referenceItem = await _dbContext.ContentReferenceItems
                .Include(r => r.Embeddings)
                .FirstOrDefaultAsync(r => r.Id == referenceId, ct);

            if (referenceItem != null)
            {
                if (referenceItem.Embeddings.Any())
                {
                    _dbContext.ContentEmbeddings.RemoveRange(referenceItem.Embeddings);
                }
                _dbContext.ContentReferenceItems.Remove(referenceItem);
            }

            await _dbContext.SaveChangesAsync(ct);
            // Refresh the cache since the set of references has changed.
            await RefreshReferencesCacheAsync();
        }

        /// <inheritdoc />
        private async Task<ContentReferenceItem> EnsureContentReferenceItemWithRagTextAsync(ContentReferenceItem reference, bool saveChanges = false)
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
                            await _dbContext.SaveChangesAsync();
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



        private async Task<float[]?> GenerateEmbeddingsForReferenceAsync(ContentReferenceItem reference)
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

            foreach (var reference in references.Where(r => !string.IsNullOrEmpty(r.RagText)))
            {
                var storedEmbeddings = await _dbContext.ContentEmbeddings
                    .Where(e => e.ContentReferenceItemId == reference.Id)
                    .OrderBy(e => e.SequenceNumber)
                    .ToListAsync();

                if (reference.RagText != null && (!storedEmbeddings.Any() ||
                    (storedEmbeddings.Any() &&
                     !AreChunksUpToDate(storedEmbeddings.Select(e => e.ChunkText).ToList(), reference.RagText, maxChunkTokens))))
                {
                    await GenerateAndStoreEmbeddingsAsync(reference, maxChunkTokens);
                    storedEmbeddings = await _dbContext.ContentEmbeddings
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
        /// Generates and stores embeddings for a content reference item.
        /// </summary>
        private async Task GenerateAndStoreEmbeddingsAsync(ContentReferenceItem reference, int maxChunkTokens)
        {
            if (string.IsNullOrEmpty(reference.RagText))
                return;

            var existingEmbeddings = await _dbContext.ContentEmbeddings
                .Where(e => e.ContentReferenceItemId == reference.Id)
                .ToListAsync();

            if (existingEmbeddings.Any())
            {
                _dbContext.ContentEmbeddings.RemoveRange(existingEmbeddings);
                await _dbContext.SaveChangesAsync();
            }

            var chunks = ChunkContent(reference.RagText, maxChunkTokens);

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                try
                {
                    var embeddingVector = await _aiEmbeddingService.GenerateEmbeddingsAsync(chunk);
                    if (embeddingVector != null && embeddingVector.Length > 0)
                    {
                        var embedding = new ContentEmbedding
                        {
                            ContentReferenceItemId = reference.Id,
                            ChunkText = chunk,
                            EmbeddingVector = SerializeEmbeddingVector(embeddingVector),
                            SequenceNumber = i,
                            GeneratedUtc = DateTime.UtcNow
                        };
                        _dbContext.ContentEmbeddings.Add(embedding);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating embedding for chunk {ChunkIndex} of reference {ReferenceId}", i, reference.Id);
                }
            }

            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Checks if stored chunks match what would be generated from the current text.
        /// </summary>
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

        /// <summary>
        /// Serializes an embedding vector to a byte array.
        /// </summary>
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

        /// <summary>
        /// Deserializes an embedding vector from a byte array.
        /// </summary>
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
                // Currently only support GeneratedDocument references.
                await AddGeneratedDocumentReferencesAsync(references);
                // Additional types can be added in future.
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
                var documents = await _dbContext.GeneratedDocuments
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
