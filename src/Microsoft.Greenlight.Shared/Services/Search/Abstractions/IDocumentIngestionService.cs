// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Services.Search.Abstractions;

/// <summary>
/// Service for handling document ingestion pipeline including chunking, embedding, and storage.
/// Abstracts the implementation details from higher-level grains and orchestration.
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Processes and ingests a document into the vector store.
    /// </summary>
    /// <param name="documentId">The ID of the ingested document entity.</param>
    /// <param name="fileStream">Stream containing the document content.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="documentUrl">URL of the document.</param>
    /// <param name="documentLibraryName">Name of the document library or process.</param>
    /// <param name="indexName">Name of the index to store in.</param>
    /// <param name="userId">Optional user ID.</param>
    /// <param name="additionalTags">Optional additional metadata tags.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task<DocumentIngestionResult> IngestDocumentAsync(
        Guid documentId,
        Stream fileStream,
        string fileName,
        string documentUrl,
        string documentLibraryName,
        string indexName,
        string? userId = null,
        Dictionary<string, string>? additionalTags = null);

    /// <summary>
    /// Deletes a document from the vector store.
    /// </summary>
    /// <param name="documentLibraryName">Name of the document library.</param>
    /// <param name="indexName">Name of the index.</param>
    /// <param name="fileName">Name of the file to delete.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task<DocumentIngestionResult> DeleteDocumentAsync(
        string documentLibraryName,
        string indexName,
        string fileName);

    /// <summary>
    /// Clears all vector data from the specified index/collection for a given document library or process.
    /// This does not delete the container in blob storage; only the vector index is cleared.
    /// </summary>
    /// <param name="documentLibraryName">Short name of the document library or document process.</param>
    /// <param name="indexName">The exact vector index/collection name to clear.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearIndexAsync(string documentLibraryName, string indexName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a document ingestion operation.
/// </summary>
public class DocumentIngestionResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of chunks created during ingestion.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Size of the document in bytes.
    /// </summary>
    public long DocumentSizeBytes { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="chunkCount">Number of chunks created.</param>
    /// <param name="documentSizeBytes">Size of the document in bytes.</param>
    /// <returns>Successful result.</returns>
    public static DocumentIngestionResult Ok(int chunkCount = 0, long documentSizeBytes = 0)
        => new() { Success = true, ChunkCount = chunkCount, DocumentSizeBytes = documentSizeBytes };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorMessage">Error message.</param>
    /// <returns>Failed result.</returns>
    public static DocumentIngestionResult Fail(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };
}
