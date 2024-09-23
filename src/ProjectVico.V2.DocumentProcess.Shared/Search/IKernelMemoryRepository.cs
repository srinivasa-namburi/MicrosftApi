using Microsoft.KernelMemory;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.DocumentProcess.Shared.Search;

public interface IKernelMemoryRepository
{

    // This is the main search method, called by all other search methods
    Task StoreContentAsync(string documentProcessName, string indexName, Stream fileStream, string fileName,
        string? documentUrl, string? userId = null, Dictionary<string, string>? additionalTags = null);

    Task DeleteContentAsync(string documentProcessName, string indexName, string fileName);
    Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(DocumentProcessInfo documentProcessInfo, string searchText, int top = 12, double minRelevance = 0.7);
    Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(DocumentProcessOptions documentProcessOptions, string searchText, int top = 12, double minRelevance = 0.7);
    Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(string documentProcessName, string searchText, int top = 12, double minRelevance = 0.7);

    Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(string documentProcessName,
        string indexName, string searchText, int top = 12, double minRelevance = 0.7);

    Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(string documentProcessName, string indexName, Dictionary<string, string> parametersExactMatch, string searchText, int top = 12, double minRelevance = 0.7);

    Task<MemoryAnswer?> AskAsync(string documentProcessName, string indexName, Dictionary<string, string> parametersExactMatch,
        string question);

}