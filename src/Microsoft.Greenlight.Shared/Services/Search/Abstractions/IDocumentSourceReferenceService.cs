// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Models.SourceReferences;

namespace Microsoft.Greenlight.Shared.Services.Search.Abstractions;

/// <summary>
/// Service for enhancing source reference items with document metadata and linking.
/// Provides bi-directional relationship between vector store results and ingested documents.
/// </summary>
public interface IDocumentSourceReferenceService
{
    /// <summary>
    /// Enhances vector store source reference items with metadata from IngestedDocument records.
    /// Provides fallback logic when linking data is missing or incomplete.
    /// </summary>
    /// <param name="sourceReferences">Source reference items from vector store search.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enhanced source reference items with document metadata.</returns>
    Task<List<SourceReferenceItem>> EnhanceWithDocumentMetadataAsync(
        List<SourceReferenceItem> sourceReferences, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets IngestedDocument information for a specific vector store document ID.
    /// Returns null if not found, allowing graceful fallback handling.
    /// </summary>
    /// <param name="vectorStoreDocumentId">The document ID used in the vector store.</param>
    /// <param name="vectorStoreIndexName">Optional index name for additional filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>IngestedDocument information or null if not found.</returns>
    Task<Microsoft.Greenlight.Shared.Models.IngestedDocument?> GetIngestedDocumentByVectorStoreIdAsync(
        string vectorStoreDocumentId,
        string? vectorStoreIndexName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple IngestedDocument records for a list of vector store document IDs.
    /// Includes fallback logic for missing or unlinked documents.
    /// </summary>
    /// <param name="vectorStoreDocumentIds">List of vector store document IDs.</param>
    /// <param name="vectorStoreIndexName">Optional index name for additional filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping vector store document ID to IngestedDocument (null if not found).</returns>
    Task<Dictionary<string, Microsoft.Greenlight.Shared.Models.IngestedDocument?>> GetIngestedDocumentsByVectorStoreIdsAsync(
        IEnumerable<string> vectorStoreDocumentIds,
        string? vectorStoreIndexName = null,
        CancellationToken cancellationToken = default);
}