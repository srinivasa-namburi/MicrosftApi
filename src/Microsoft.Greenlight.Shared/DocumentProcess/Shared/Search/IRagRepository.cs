using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Search;

public interface IRagRepository
{
    bool CreateOrUpdateRepository();
    bool ClearRepositoryContent();
    /// <summary>
    /// Abstract method - implement this method to search the repository for the given searchText.
    /// For some document processes, this may involve searching multiple indexes.
    /// A list of search results is returned, with each result containing the repository name and a list of documents.
    /// </summary>
    /// <param name="searchText">Text to search for</param>
    /// <param name="top">Number of results to return (for each repository) - default 12</param>
    /// <param name="k"></param>
    /// <returns></returns>
    Task<List<RagRepositorySearchResult>> SearchAsync(string searchText, int top = 12, int k = 7);
    Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName, Stream streamForHashing);
    Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName, string fileHash);
}
