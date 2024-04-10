using Azure.Search.Documents;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Interfaces;

public interface IIndexingProcessor
{
    bool CreateOrUpdateIndex(string indexName);
    bool DeleteIndex(string indexName);
    
    Task<List<ReportDocument>> SearchSpecifiedIndexAsync(string indexName, string searchText, int top = 12, int k = 7);
   
    /// <summary>
    /// This method is used to create a JSON string from a ContentNode object. This JSON string is then used to index the content in Azure Search.
    /// All Children below the ContentNode are also included in the JSON string, in a recursive fashion (nested Children).
    /// </summary>
    /// <param name="contentNode"></param>
    /// <param name="parentId"></param>
    /// <param name="parentTitle"></param>
    /// <param name="fileName"></param>
    /// <param name="fileHash"></param>
    /// <returns></returns>
    string CreateJsonFromContentNode(ContentNode contentNode, Guid? parentId, string? parentTitle,
        string fileName, string fileHash);

    /// <summary>
    /// This variation of IndexJson allows you to specify the SearchClient to use - which also selects the index to use as that is tied to the SearchClient.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="searchClientWithIndex"></param>
    /// <param name="generateEmbeddings"></param>
    /// <returns></returns>
    Task<bool> IndexJson(string json, SearchClient searchClientWithIndex, bool generateEmbeddings = false);

    /// <summary>
    /// Returns a SearchClient for the specified index.
    /// </summary>
    /// <param name="indexName"></param>
    /// <returns></returns>
    SearchClient GetSearchClient(string indexName);

    bool IsEmptyIndex(string indexName);
}