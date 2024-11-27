using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

public class AdditionalDocumentLibraryKernelMemoryRepository : IAdditionalDocumentLibraryKernelMemoryRepository
{
    private readonly IKernelMemoryRepository _baseKernelMemoryRepository;

    public AdditionalDocumentLibraryKernelMemoryRepository(
        [FromKeyedServices("AdditionalBase-IKernelMemoryRepository")]
        IKernelMemoryRepository baseKernelMemoryRepository
    )
    {
        _baseKernelMemoryRepository = baseKernelMemoryRepository;
    }
    public async Task StoreContentAsync(string documentLibraryName, string indexName, Stream fileStream, string fileName,
        string? documentUrl, string? userId = null, Dictionary<string, string>? additionalTags = null)
    {
        documentLibraryName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        await _baseKernelMemoryRepository.StoreContentAsync(documentLibraryName, indexName, fileStream, fileName, documentUrl, userId, additionalTags);
    }

    public async Task DeleteContentAsync(string documentLibraryName, string indexName, string fileName)
    {
        documentLibraryName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        await _baseKernelMemoryRepository.DeleteContentAsync(documentLibraryName, indexName, fileName);
    }

    public async Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(string documentLibraryName, string searchText, int top = 12, double minRelevance = 0.7)
    {
        documentLibraryName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        // We need to determine the index name from the document library name
        var indexName = "index-" + documentLibraryName.ToLower()
            .Replace(" ", "-")
            .Replace(".", "-");

        var result = await SearchAsyncInIndex(documentLibraryName, indexName, searchText, top, minRelevance);
        return result;
    }

    public async Task<MemoryAnswer?> AskAsync(string documentLibraryName, string indexName, string question)
    {
        documentLibraryName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        var result = await _baseKernelMemoryRepository.AskAsync(documentLibraryName, indexName, null, question);
        return result;
    }

    private Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsyncInIndex(string documentLibraryName, string indexName, string searchText, int top = 12,
        double minRelevance = 0.7)
    {
        documentLibraryName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        var result = _baseKernelMemoryRepository.SearchAsync(documentLibraryName, indexName, searchText, top, minRelevance);
        return result;
    }

    private string GetDocumentLibraryInternalIdentifier(string documentLibraryName)
    {
        if (!documentLibraryName.StartsWith("Additional-"))
        {
            documentLibraryName = "Additional-" + documentLibraryName;
        }

        return documentLibraryName;
    }
}