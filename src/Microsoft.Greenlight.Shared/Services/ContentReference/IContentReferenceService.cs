// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <summary>
    /// Content Reference service to query, create and manage content reference items and their derived (RAG) text.
    /// </summary>
    public interface IContentReferenceService
    {
        /// <summary>
        /// Retrieves all content reference items excluding <see cref="ContentReferenceType.ExternalFile"/>.
        /// This is a potentially expensive call (full table materialization) and should be avoided on user facing hot paths.
        /// </summary>
        /// <returns>A list of <see cref="ContentReferenceItemInfo"/> objects or an empty list if none exist.</returns>
        Task<List<ContentReferenceItemInfo>?> GetAllReferences();

        /// <summary>
        /// Performs a case-insensitive search over content reference display names (excluding ExternalFile).
        /// </summary>
        /// <param name="searchTerm">The search term. If null or whitespace all references are returned.</param>
        /// <returns>Matching <see cref="ContentReferenceItemInfo"/> entries or an empty list.</returns>
        Task<List<ContentReferenceItemInfo>?> SearchReferencesAsync(string searchTerm);

        /// <summary>
        /// Performs semantic similarity search (vector store) across references. Falls back to text search if vector search fails or yields no matches.
        /// </summary>
        /// <param name="searchTerm">The natural language search phrase.</param>
        /// <param name="maxResults">Maximum number of results to return (minimum 1).</param>
        /// <returns>Similar references ordered by relevance or an empty list.</returns>
        Task<List<ContentReferenceItemInfo>?> SearchSimilarReferencesAsync(string searchTerm, int maxResults = 5);

        /// <summary>
        /// Retrieves a single reference by identifier and expected type (excluding ExternalFile).
        /// </summary>
        /// <param name="id">The reference identifier.</param>
        /// <param name="type">Expected <see cref="ContentReferenceType"/>.</param>
        /// <returns>The matching <see cref="ContentReferenceItemInfo"/> or null if not found.</returns>
        Task<ContentReferenceItemInfo?> GetReferenceByIdAsync(Guid id, ContentReferenceType type);

        /// <summary>
        /// Retrieves a reference by its source entity identifier and type (excluding ExternalFile).
        /// Optimized for UI flows that previously performed get-all and client-side search.
        /// </summary>
        /// <param name="sourceId">The source entity identifier the reference was created from.</param>
        /// <param name="type">The reference type.</param>
        /// <returns>The matching <see cref="ContentReferenceItemInfo"/> or null if not found.</returns>
        Task<ContentReferenceItemInfo?> GetBySourceIdAsync(Guid sourceId, ContentReferenceType type);

        /// <summary>
        /// Returns a lightweight list of references for the AI Assistant selector.
        /// Defaults to internal types only (excludes ExternalFile) and returns most recent first.
        /// Results may be cached in-process briefly for responsiveness.
        /// </summary>
        /// <param name="top">Maximum items to return.</param>
        /// <param name="types">Optional filter for reference types; when null excludes ExternalFile by default.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<List<ContentReferenceItemInfo>> GetAssistantReferenceListAsync(int top = 200, ContentReferenceType[]? types = null, CancellationToken ct = default);

        /// <summary>
        /// Invalidates the short-lived cache used for the assistant reference list.
        /// </summary>
        Task InvalidateAssistantReferenceListCacheAsync(CancellationToken ct = default);

        /// <summary>
        /// Rebuilds and stores the in-memory list of references (legacy behavior). No-op for distributed cache.
        /// </summary>
        Task RefreshReferencesCacheAsync();

        /// <summary>
        /// Gets previously generated or stored RAG (retrieval augmented generation) text for a reference.
        /// </summary>
        /// <param name="id">Reference identifier.</param>
        /// <returns>The RAG text or null if not present.</returns>
        Task<string?> GetRagTextAsync(Guid id);

        /// <summary>
        /// Looks up an existing <see cref="ContentReferenceItem"/> (by Id or forgiving source Id for some types) or creates a new one.
        /// Will attempt to populate RAG text lazily.
        /// </summary>
        /// <param name="id">Content reference Id (or source Id for forgiving types).</param>
        /// <param name="type">The reference type.</param>
        /// <returns>The existing or newly created entity.</returns>
        Task<ContentReferenceItem> GetOrCreateContentReferenceItemAsync(Guid id, ContentReferenceType type);

        /// <summary>
        /// Materializes a set of <see cref="ContentReferenceItem"/> rows by explicit identifiers (no auto-create).
        /// Ensures RAG text is generated for returned entities when missing.
        /// </summary>
        /// <param name="ids">List of reference identifiers.</param>
        /// <returns>List of existing entities (subset of requested IDs).</returns>
        Task<List<ContentReferenceItem>> GetContentReferenceItemsFromIdsAsync(List<Guid> ids);

        /// <summary>
        /// Scans underlying source entities and creates, updates or removes reference rows accordingly.
        /// Currently processes generated document references.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task ScanAndUpdateReferencesAsync(CancellationToken ct = default);

        /// <summary>
        /// Removes a content reference item (and related embeddings) by identifier.
        /// </summary>
        /// <param name="referenceId">Reference identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        Task RemoveReferenceAsync(Guid referenceId, CancellationToken ct = default);

        /// <summary>
        /// Generates or retrieves the RAG text for a specific content reference entity (in-memory entity instance required).
        /// </summary>
        /// <param name="reference">The loaded <see cref="ContentReferenceItem"/> entity.</param>
        /// <returns>RAG text content or null if generation fails.</returns>
        Task<string?> GetContentTextForContentReferenceItem(ContentReferenceItem reference);

        /// <summary>
        /// Creates a ContentReferenceItem for an ExternalLinkAsset uploaded file.
        /// Handles deduplication, file acknowledgment, and RAG text generation.
        /// </summary>
        /// <param name="externalLinkAssetId">The ID of the ExternalLinkAsset created by FileStorageService.</param>
        /// <param name="fileName">The original file name.</param>
        /// <param name="fileHash">Optional file hash for deduplication.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The created or existing ContentReferenceItem as DTO.</returns>
        Task<ContentReferenceItemInfo> CreateExternalLinkAssetReferenceAsync(
            Guid externalLinkAssetId, 
            string fileName, 
            string? fileHash = null, 
            CancellationToken ct = default);

        /// <summary>
        /// Creates a ContentReferenceItem for an ExternalFile (legacy ExportedDocumentLink).
        /// Handles deduplication based on file hash and ensures FileAcknowledgmentRecords are created.
        /// </summary>
        /// <param name="exportedDocumentLink">The ExportedDocumentLink entity.</param>
        /// <param name="fileName">The original file name.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The created or existing ContentReferenceItem as DTO.</returns>
        Task<ContentReferenceItemInfo> CreateExternalFileReferenceAsync(
            ExportedDocumentLink exportedDocumentLink, 
            string fileName, 
            CancellationToken ct = default);

        /// <summary>
        /// Checks for duplicate ContentReferenceItems based on file hash.
        /// Used for file deduplication during uploads.
        /// </summary>
        /// <param name="fileHash">The file hash to check.</param>
        /// <param name="referenceType">The type of reference to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Existing ContentReferenceItem if duplicate found, null otherwise.</returns>
        Task<ContentReferenceItem?> FindDuplicateByFileHashAsync(
            string fileHash, 
            ContentReferenceType referenceType, 
            CancellationToken ct = default);

        /// <summary>
        /// Creates a fallback ContentReferenceItem when file processing fails.
        /// Used to maintain user experience even when file upload/processing encounters errors.
        /// </summary>
        /// <param name="fileName">The original file name.</param>
        /// <param name="referenceType">The type of reference to create.</param>
        /// <param name="errorMessage">Optional error message to include in description.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The created ContentReferenceItem as DTO.</returns>
        Task<ContentReferenceItemInfo> CreateFallbackReferenceAsync(
            string fileName, 
            ContentReferenceType referenceType,
            string? errorMessage = null,
            CancellationToken ct = default);

        /// <summary>
        /// Creates a ContentReferenceItem for a ReviewInstance.
        /// Reviews themselves become ContentReferenceItems that can be referenced in chat/generation.
        /// </summary>
        /// <param name="reviewInstanceId">The ID of the ReviewInstance.</param>
        /// <param name="displayName">Display name for the review reference.</param>
        /// <param name="fileHash">Optional file hash if the review has an associated document.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The created ContentReferenceItem.</returns>
        Task<ContentReferenceItem> CreateReviewReferenceAsync(
            Guid reviewInstanceId,
            string displayName,
            string? fileHash = null,
            CancellationToken ct = default);

        /// <summary>
        /// Gets ContentReferenceItems for a ReviewInstance.
        /// Returns the review's ContentReferenceItem with RAG text populated.
        /// </summary>
        /// <param name="reviewInstanceId">The ID of the ReviewInstance.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of ContentReferenceItems for the review (typically one).</returns>
        Task<List<ContentReferenceItem>> GetReviewContentReferenceItemsAsync(
            Guid reviewInstanceId,
            CancellationToken ct = default);
    }
}
