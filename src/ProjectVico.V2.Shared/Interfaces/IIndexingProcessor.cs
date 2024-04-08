using Azure.Search.Documents;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Interfaces;

public interface IIndexingProcessor
{
    bool CreateOrUpdateIndex(string indexName);
    bool DeleteIndex(string indexName);
    
    Task<List<ReportDocument>> SearchWithHybridSearchAsync(string searchText, int top = 12, int k = 7);
    Task<List<ReportDocument>> SearchSpecifiedIndexAsync(string indexName, string searchText, int top = 12, int k = 7);
    Task IndexAndStoreContentNodesAsync(List<ContentNode> contentTree, string baseFileName, Stream streamForHashing);
    Task IndexAndStoreContentNodesAsync(List<ContentNode> contentTree, string baseFileName, string fileHash);
    Task IndexAndStoreCustomNodesAsync(List<ContentNode> contentTree, string baseFileName, Stream streamForHashing);

    Task<IEnumerable<ReportDocument>> GetAllUniqueTitlesAsync(int numberOfUniqueFiles);
    Task<List<ReportDocument>> SearchWithTitleSearchAsync(string searchText, int top = 12, int k = 7);
    Task<List<ReportDocument>> SearchWithCustomSearchAsync(string searchText, int top = 12, int k = 7);

    string CreateJsonFromContentNode(ContentNode contentNode, Guid? parentId, string? parentTitle, string fileName, Stream hashStream);

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

    SearchClient GetSearchClient(string indexName);
}