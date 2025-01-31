using Azure.Search.Documents;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Interfaces;

/// <summary>
/// Interface for processing indexing operations.
/// </summary>
public interface IIndexingProcessor
{
    /// <summary>
    /// Creates or updates an index with the specified name.
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <returns>True if the operation is successful, otherwise false.</returns>
    bool CreateOrUpdateIndex(string indexName);

    /// <summary>
    /// Deletes the index with the specified name.
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <returns>True if the operation is successful, otherwise false.</returns>
    bool DeleteIndex(string indexName);

    /// <summary>
    /// Searches the specified index asynchronously.
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="top">The number of results to return.</param>
    /// <param name="k">The number of results to consider for ranking.</param>
    /// <returns>A list of report documents matching the search criteria.</returns>
    Task<List<ReportDocument>> SearchSpecifiedIndexAsync(string indexName, string searchText, int top = 12, int k = 7);

    /// <summary>
    /// This method is used to create a JSON string from a ContentNode object. 
    /// This JSON string is then used to index the content in Azure Search.
    /// All Children below the ContentNode are also included in the JSON string, 
    /// in a recursive fashion (nested Children).
    /// </summary>
    /// <param name="contentNode">The content node to convert to JSON.</param>
    /// <param name="parentId">The ID of the parent node.</param>
    /// <param name="parentTitle">The title of the parent node.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="fileHash">The hash of the file.</param>
    /// <returns>A JSON string representing the content node.</returns>
    string CreateJsonFromContentNode(ContentNode contentNode, Guid? parentId, string? parentTitle, string fileName, string fileHash);

    /// <summary>
    /// Indexes a JSON string using the specified search client.
    /// </summary>
    /// <param name="json">The JSON string to index.</param>
    /// <param name="searchClientWithIndex">The search client to use for indexing.</param>
    /// <param name="generateEmbeddings">Whether to generate embeddings for the indexed content.</param>
    /// <returns>True if the operation is successful, otherwise false.</returns>
    Task<bool> IndexJson(string json, SearchClient searchClientWithIndex, bool generateEmbeddings = false);

    /// <summary>
    /// Gets a search client for the specified index.
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <returns>A search client for the specified index.</returns>
    SearchClient GetSearchClient(string indexName);

    /// <summary>
    /// Checks if the specified index is empty.
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <returns>True if the index is empty, otherwise false.</returns>
    bool IsEmptyIndex(string indexName);
}
