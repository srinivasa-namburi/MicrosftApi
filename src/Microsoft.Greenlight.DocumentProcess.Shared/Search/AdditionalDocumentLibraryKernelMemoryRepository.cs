using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

public class AdditionalDocumentLibraryKernelMemoryRepository : IAdditionalDocumentLibraryKernelMemoryRepository
{
    private readonly IKernelMemoryRepository _baseKernelMemoryRepository;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;

    public AdditionalDocumentLibraryKernelMemoryRepository(
        [FromKeyedServices("AdditionalBase-IKernelMemoryRepository")]
        IKernelMemoryRepository baseKernelMemoryRepository,
        IDocumentLibraryInfoService documentLibraryInfoService
    )
    {
        _baseKernelMemoryRepository = baseKernelMemoryRepository;
        _documentLibraryInfoService = documentLibraryInfoService;
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

    public async Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchAsync(string documentLibraryName, string searchText,
        int top = 12, double minRelevance = 0.7)
    {
        var documentLibraryInternalName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        // We need to determine the index name from the document library name
        var documentLibraryInfo =
            await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryName);

        if (documentLibraryInfo == null)
        {
            throw new Exception($"Document library {documentLibraryName} not found");
        }

        var indexName = documentLibraryInfo.IndexName;

        var result = await SearchAsyncInIndex(documentLibraryInternalName, indexName, searchText, top, minRelevance);
        return result;
    }

    public async Task<MemoryAnswer?> AskAsync(string documentLibraryName, string indexName, string question)
    {
        documentLibraryName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        var result = await _baseKernelMemoryRepository.AskAsync(documentLibraryName, indexName, null, question);
        return result;
    }

    private Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchAsyncInIndex(string documentLibraryName, string indexName, string searchText, int top = 12,
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