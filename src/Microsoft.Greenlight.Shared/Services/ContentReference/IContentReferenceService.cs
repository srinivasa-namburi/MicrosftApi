using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <summary>
    /// Content Reference service to get and generate content references
    /// </summary>
    public interface IContentReferenceService
    {
        /// <summary>
        /// Gets all cached content reference items.
        /// </summary>
        /// <returns>A list of cached content reference items.</returns>
        Task<List<ContentReferenceItemInfo>?> GetAllCachedReferencesAsync();

        /// <summary>
        /// Searches cached content reference items by a search term.
        /// </summary>
        /// <param name="searchTerm">The term to search for.</param>
        /// <returns>A list of matching cached content reference items.</returns>
        Task<List<ContentReferenceItemInfo>?> SearchCachedReferencesAsync(string searchTerm);

        /// <summary>
        /// Searches for similar cached content reference items by a search term.
        /// </summary>
        /// <param name="searchTerm">The term to search for.</param>
        /// <param name="maxResults">The maximum number of results to return.</param>
        /// <returns>A list of similar cached content reference items.</returns>
        Task<List<ContentReferenceItemInfo>?> SearchSimilarCachedReferencesAsync(string searchTerm, int maxResults = 5);

        /// <summary>
        /// Gets a cached content reference item by its ID and type.
        /// </summary>
        /// <param name="id">The ID of the content reference item.</param>
        /// <param name="type">The type of the content reference item.</param>
        /// <returns>The cached content reference item.</returns>
        Task<ContentReferenceItemInfo?> GetCachedReferenceByIdAsync(Guid id, ContentReferenceType type);

        /// <summary>
        /// Refreshes the cache of content reference items.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RefreshReferencesCacheAsync();

        /// <summary>
        /// Gets the RAG text for a content reference item by its ID.
        /// </summary>
        /// <param name="id">The ID of the content reference item.</param>
        /// <returns>The RAG text.</returns>
        Task<string?> GetRagTextAsync(Guid id);

        /// <summary>
        /// Gets or creates a content reference item by its ID and type.
        /// </summary>
        /// <param name="id">The ID of the content reference item.</param>
        /// <param name="type">The type of the content reference item.</param>
        /// <returns>The content reference item.</returns>
        Task<ContentReferenceItem> GetOrCreateContentReferenceItemAsync(Guid id, ContentReferenceType type);

        /// <summary>
        /// Gets content reference items from a list of IDs.
        /// </summary>
        /// <param name="ids">The list of IDs.</param>
        /// <returns>A list of content reference items.</returns>
        Task<List<ContentReferenceItem>> GetContentReferenceItemsFromIdsAsync(List<Guid> ids);

        /// <summary>
        /// Generates embeddings for a list of content chunks.
        /// </summary>
        /// <param name="chunks">The list of content chunks.</param>
        /// <returns>A dictionary of embeddings for the content chunks.</returns>
        Task<Dictionary<string, float[]>> GenerateEmbeddingsForChunksAsync(List<string> chunks);

        /// <summary>
        /// Generates embeddings for a query.
        /// </summary>
        /// <param name="query">The query string.</param>
        /// <returns>The embeddings for the query.</returns>
        Task<float[]> GenerateEmbeddingsForQueryAsync(string query);

        /// <summary>
        /// Calculates similarity scores between a query embedding and chunk embeddings.
        /// </summary>
        /// <param name="queryEmbedding">The query embedding.</param>
        /// <param name="chunkEmbeddings">The chunk embeddings.</param>
        /// <returns>A list of tuples containing chunks and their similarity scores.</returns>
        List<(string Chunk, float Score)> CalculateSimilarityScores(float[] queryEmbedding, Dictionary<string, float[]> chunkEmbeddings);

        /// <summary>
        /// Selects the top N chunks based on similarity scores.
        /// </summary>
        /// <param name="similarityScores">The list of similarity scores.</param>
        /// <param name="topN">The number of top chunks to select.</param>
        /// <returns>A list of the top N chunks.</returns>
        List<string> SelectTopChunks(List<(string Chunk, float Score)> similarityScores, int topN);

        /// <summary>
        /// Chunks content into smaller pieces based on a maximum number of tokens.
        /// </summary>
        /// <param name="content">The content to chunk.</param>
        /// <param name="maxTokens">The maximum number of tokens per chunk.</param>
        /// <returns>A list of content chunks.</returns>
        List<string> ChunkContent(string content, int maxTokens);

        /// <summary>
        /// Retrieves or generates embeddings for a list of content reference items.
        /// </summary>
        /// <param name="references">The list of content reference items.</param>
        /// <param name="maxChunkTokens">The maximum number of tokens per chunk.</param>
        /// <returns>A dictionary of embeddings for the content reference items.</returns>
        Task<Dictionary<(Guid ReferenceId, string Chunk), float[]>> GetOrCreateEmbeddingsForContentAsync(
            List<ContentReferenceItem> references,
            int maxChunkTokens = 1200);

        /// <summary>
        /// Scans for and updates content reference items for all supported content types.
        /// For now, this method handles only GeneratedDocument references.
        /// It creates new references for documents that do not yet have one and removes stale references.
        /// </summary>
        Task ScanAndUpdateReferencesAsync(CancellationToken ct = default);

        /// <summary>
        /// Removes a ContentReferenceItem and refreshes the cache
        /// </summary>>
        Task RemoveReferenceAsync(Guid referenceId, CancellationToken ct = default);

        /// <inheritdoc />
        Task<string?> GetContentTextForContentReferenceItem(ContentReferenceItem reference);
    }
}