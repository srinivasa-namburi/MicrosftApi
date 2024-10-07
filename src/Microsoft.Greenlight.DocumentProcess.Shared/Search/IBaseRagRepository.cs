using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

public interface IBaseRagRepository : IRagRepository
{
    /// <summary>
    /// Returns a list of search results across all repositories for the given Document Process.
    /// </summary>
    /// <param name="documentProcessOptions">Options for the Document Process being searched</param>
    /// <param name="searchText">Text to search for</param>
    /// <param name="top">Number of results to return (for each repository) - default 12</param>
    /// <param name="k"></param>
    /// <returns></returns>
    Task<List<RagRepositorySearchResult>> SearchAsync(DocumentProcessOptions documentProcessOptions, string searchText,
        int top = 12, int k = 7);
}
