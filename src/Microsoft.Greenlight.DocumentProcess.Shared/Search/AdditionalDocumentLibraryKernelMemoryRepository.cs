using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

public class AdditionalDocumentLibraryKernelMemoryRepository : IAdditionalDocumentLibraryKernelMemoryRepository
{
    private readonly IKernelMemoryRepository _baseKernelMemoryRepository;
    private readonly IConsolidatedSearchOptionsFactory _searchOptionsFactory;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;

    public AdditionalDocumentLibraryKernelMemoryRepository(
        [FromKeyedServices("AdditionalBase-IKernelMemoryRepository")]
        IKernelMemoryRepository baseKernelMemoryRepository,
        IConsolidatedSearchOptionsFactory searchOptionsFactory,
        IDocumentLibraryInfoService documentLibraryInfoService
    )
    {
        _baseKernelMemoryRepository = baseKernelMemoryRepository;
        _searchOptionsFactory = searchOptionsFactory;
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

    public async Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchAsync(string documentLibraryName, string searchText)
    {
        var documentLibraryInternalName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        // We need to determine the index name from the document library name
        var documentLibraryInfo =
            await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryName);

        if (documentLibraryInfo == null)
        {
            throw new Exception($"Document library {documentLibraryName} not found");
        }
        
        var searchOptions = await _searchOptionsFactory.CreateSearchOptionsForDocumentLibraryAsync(documentLibraryInfo);
        var result = await SearchAsyncInIndex(documentLibraryInternalName, searchText, searchOptions);

        return result;
    }

    public async Task<MemoryAnswer?> AskAsync(string documentLibraryName, string indexName, string question)
    {
        documentLibraryName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        var result = await _baseKernelMemoryRepository.AskAsync(documentLibraryName, indexName, null, question);
        return result;
    }

    private async Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchAsyncInIndex(
        string documentLibraryName, string searchText, ConsolidatedSearchOptions searchOptions)
    {
        documentLibraryName = GetDocumentLibraryInternalIdentifier(documentLibraryName);
        var result = await _baseKernelMemoryRepository.SearchAsync(documentLibraryName, searchText, searchOptions);
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