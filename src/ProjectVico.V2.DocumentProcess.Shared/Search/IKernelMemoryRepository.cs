using Microsoft.KernelMemory;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.DocumentProcess.Shared.Search;

public interface IKernelMemoryRepository
{
    Task StoreContentAsync(string documentProcessName, string indexName, Stream fileStream, string fileName,
        string? documentUrl, string? userId = null);
    Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(DocumentProcessOptions documentProcessOptions, string searchText, int top = 12, double minRelevance = 0.7);
    Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(string documentProcessName, string searchText, int top = 12, double minRelevance = 0.7);
}