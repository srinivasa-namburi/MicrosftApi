using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

public interface IAdditionalDocumentLibraryKernelMemoryRepository
{
    Task StoreContentAsync(string documentLibraryName, string indexName, Stream fileStream, string fileName,
        string? documentUrl, string? userId = null, Dictionary<string, string>? additionalTags = null);

    Task DeleteContentAsync(string documentLibraryName, string indexName, string fileName);
    Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchAsync(string documentLibraryName, string searchText);
    Task<MemoryAnswer?> AskAsync(string documentLibraryName, string indexName, string question);

}