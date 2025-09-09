// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Models.SourceReferences;

namespace Microsoft.Greenlight.Shared.Services.Search.Abstractions;

/// <summary>
/// Generic interface for document ingestion, search, and storage operations.
/// Abstracts away the underlying implementation (Kernel Memory vs Semantic Kernel Vector Store).
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Stores document content in the repository.
    /// </summary>
    /// <param name="documentLibraryName">Name of the document library.</param>
    /// <param name="indexName">Name of the index.</param>
    /// <param name="fileStream">Stream containing the file data.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="documentReference">Document reference identifier for dynamic URL resolution.</param>
    /// <param name="userId">Optional user ID.</param>
    /// <param name="additionalTags">Optional additional metadata tags.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task StoreContentAsync(string documentLibraryName, string indexName, Stream fileStream, string fileName,
        string? documentReference, string? userId = null, Dictionary<string, string>? additionalTags = null);

    /// <summary>
    /// Deletes document content from the repository.
    /// </summary>
    /// <param name="documentLibraryName">Name of the document library.</param>
    /// <param name="indexName">Name of the index.</param>
    /// <param name="fileName">Name of the file to delete.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task DeleteContentAsync(string documentLibraryName, string indexName, string fileName);

    /// <summary>
    /// Searches for documents in the repository.
    /// </summary>
    /// <param name="documentLibraryName">Name of the document library.</param>
    /// <param name="searchText">Search query.</param>
    /// <param name="options">Search options.</param>
    /// <returns>List of search results.</returns>
    Task<List<SourceReferenceItem>> SearchAsync(
        string documentLibraryName,
        string searchText,
        ConsolidatedSearchOptions options);

    /// <summary>
    /// Asks a question to the repository and gets an answer.
    /// </summary>
    /// <param name="documentLibraryName">Name of the document library.</param>
    /// <param name="indexName">Name of the index.</param>
    /// <param name="parametersExactMatch">Optional exact match parameters.</param>
    /// <param name="question">Question to ask.</param>
    /// <returns>Answer to the question.</returns>
    Task<DocumentRepositoryAnswer?> AskAsync(string documentLibraryName, string indexName, 
        Dictionary<string, string>? parametersExactMatch, string question);
}