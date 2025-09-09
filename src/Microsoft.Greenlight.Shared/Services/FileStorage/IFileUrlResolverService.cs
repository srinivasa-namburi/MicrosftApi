// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Services.FileStorage;

/// <summary>
/// Service for dynamically resolving file URLs to proxied API endpoints.
/// Provides a centralized way to generate URLs for files from various storage sources.
/// </summary>
public interface IFileUrlResolverService
{
    /// <summary>
    /// Resolves a file URL for an IngestedDocument, creating ExternalLinkAsset if needed.
    /// </summary>
    /// <param name="ingestedDocument">The ingested document to resolve URL for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Proxied URL that routes through the API.</returns>
    Task<string> ResolveUrlAsync(IngestedDocument ingestedDocument, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a file URL for a FileAcknowledgmentRecord, creating ExternalLinkAsset if needed.
    /// </summary>
    /// <param name="acknowledgmentRecord">The file acknowledgment record to resolve URL for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Proxied URL that routes through the API.</returns>
    Task<string> ResolveUrlAsync(FileAcknowledgmentRecord acknowledgmentRecord, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a file URL by document ID, loading the IngestedDocument from database.
    /// </summary>
    /// <param name="documentId">The document ID to resolve URL for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Proxied URL that routes through the API, or null if document not found.</returns>
    Task<string?> ResolveUrlByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a file URL by vector store document ID and index name.
    /// </summary>
    /// <param name="vectorStoreDocumentId">The vector store document ID.</param>
    /// <param name="indexName">The vector store index name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Proxied URL that routes through the API, or null if document not found.</returns>
    Task<string?> ResolveUrlByVectorStoreIdAsync(string vectorStoreDocumentId, string indexName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a file URL for a ContentReferenceItem by its ID. Handles ExternalFile via FileAcknowledgmentRecord
    /// and ExternalLinkAsset via its asset ID.
    /// </summary>
    /// <param name="contentReferenceItemId">The content reference item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Proxied URL that routes through the API, or null if not resolvable.</returns>
    Task<string?> ResolveUrlForContentReferenceAsync(Guid contentReferenceItemId, CancellationToken cancellationToken = default);
}
