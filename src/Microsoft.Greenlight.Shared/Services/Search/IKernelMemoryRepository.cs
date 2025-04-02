using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Services.Search;

public interface IKernelMemoryRepository
{

    // This is the main search method, called by all other search methods
    Task StoreContentAsync(string documentLibraryName, string indexName, Stream fileStream, string fileName,
        string? documentUrl, string? userId = null, Dictionary<string, string>? additionalTags = null);

    Task DeleteContentAsync(string documentLibraryName, string indexName, string fileName);
    
    Task<MemoryAnswer?> AskAsync(string documentLibraryName, string indexName, Dictionary<string, string>? parametersExactMatch,
        string question);

    Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchAsync(
        string documentLibraryName,
        string searchText,
        ConsolidatedSearchOptions options);
}
