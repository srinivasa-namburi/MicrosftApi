using System.ComponentModel;
using AutoMapper;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;

namespace Microsoft.Greenlight.Plugins.Default.DocumentLibrary;

public class DocumentLibraryPlugin : IPluginImplementation
{
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly IAdditionalDocumentLibraryKernelMemoryRepository _kmRepository;
    private readonly IMapper _mapper;

    public DocumentLibraryPlugin(
        IDocumentLibraryInfoService documentLibraryInfoService,
        IAdditionalDocumentLibraryKernelMemoryRepository kmRepository,
        IMapper mapper
        )
    {
        _documentLibraryInfoService = documentLibraryInfoService;
        _kmRepository = kmRepository;
        _mapper = mapper;
    }
    [KernelFunction("GetDocumentLibraryInfo")]
    [Description("Returns a list of all available document libraries with information about their contents and when to use them. " +
                 "Use to determine your knowledge capabilities")]

    public async Task<List<DocumentLibraryUsageInfo>> GetDocumentLibraryInfoAsync(
        [Description("The DocumentProcessName of the document process to filter the document libraries by. This is required.")]
        string documentProcessName)
    {
        documentProcessName = documentProcessName.TrimEnd('.').TrimEnd(); // removes any trailing periods or whitespace from the document process name
        var documentLibraries = await _documentLibraryInfoService.GetAllDocumentLibrariesAsync();

        var documentLibrariesForDocumentProcess = documentLibraries.Where(dl =>
            dl.DocumentProcessAssociations.Any(dpa => dpa.DocumentProcessShortName == documentProcessName)).ToList();

        var simplifiedDocumentLibraryList = new List<DocumentLibraryUsageInfo>();
        foreach (var docLibrary in documentLibrariesForDocumentProcess)
        {
            simplifiedDocumentLibraryList.Add(_mapper.Map<DocumentLibraryUsageInfo>(docLibrary));
        }

        return simplifiedDocumentLibraryList;
    }

    [KernelFunction("AskQuestionFromDocumentLibrary")]
    [Description("Answer a question from the contents of a document library. Do not search without a valid documentLibraryShortName " +
                 "which you MUST retrieve from 'GetDocumentLibraryInfo'")]
    public async Task<string> AskQuestionFromDocumentLibraryAsync(
        [Description("The short name of the document library to search. " +
                     "You MUST retrieve these shortnames using the plugin method 'GetDocumentLibraryInfo' BEFORE calling this method. The libraries " +
                     "in that list are the ONLY valid libraries for this method. Don't call this method without the correct documentLibraryShortName." +
                     "DON'T use the index name - use only the documentLibraryShortName")]
        string documentLibraryShortName,
        [Description("The index name to use for the search. You can retrieve the index names for all document libraries with the method 'GetDocumentLibraryInfo'")]
        string indexName,
        [Description("The question to ask the document library. This must be formulated as an actual question.")]
        string questionText
        )
    {

        // The AI sometimes gets confused with document library names and index names.
        // If the document library name starts with index- then we need to search the document libraries to get the correct document library.

        if (documentLibraryShortName.StartsWith("index-"))
        {
            var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByIndexNameAsync(documentLibraryShortName);
            if (documentLibrary == null)
            {
                return "For assistant : Wrong Document Library ShortName provided - no valid library found. Please use the DocumentProcessShortName returned by GetDocumentLibraryInfo";
            }
            documentLibraryShortName = documentLibrary.ShortName;
        }

        var memoryAnswer = await _kmRepository.AskAsync(documentLibraryShortName, indexName, questionText);
        if (memoryAnswer?.Result == null || memoryAnswer?.Result == "INFO NOT FOUND")
        {
            return "No answer found";
        }

        var resultText = memoryAnswer!.Result;
        return resultText;
    }

    [KernelFunction("SearchDocumentLibrary")]
    [Description("Search the contents of a document library. Do not search without a valid documentLibraryShortName " +
                 "which you MUST retrieve from 'GetDocumentLibraryInfo'")]
    public async Task<List<string>> SearchDocumentLibraryAsync(
            [Description("The short name of the document library to search. " +
                         "You MUST retrieve these shortnames using the plugin method 'GetDocumentLibraryInfo' BEFORE calling this method. The libraries " +
                         "in that list are the ONLY valid libraries for this method. Don't call this method without the correct documentLibraryShortName." +
                         "DON'T use the index name - use only the documentLibraryShortName")]
            string documentLibraryShortName,
            [Description("The search text to use for the search")]
            string searchText,
            [Description("The number of results to return")]
            int top = 12,
            [Description("The minimum relevance score for a result to be returned")]
            double minRelevance = 0.7
        )
    {
        if (documentLibraryShortName.StartsWith("index-"))
        {
            var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByIndexNameAsync(documentLibraryShortName);
            if (documentLibrary == null)
            {
                return ["For assistant : Wrong Document Library ShortName provided - no valid library found. Please use the DocumentProcessShortName returned by GetDocumentLibraryInfo"];
            }
            documentLibraryShortName = documentLibrary.ShortName;
        }

        var searchResults = await _kmRepository.SearchAsync(documentLibraryShortName, searchText, top, minRelevance);

        List<string> searchResultStrings = new();
        foreach (var searchResult in searchResults)
        {
            foreach (var searchResultPartition in searchResult)
            {
                searchResultStrings.Add(searchResultPartition.Value.Text);
            }
        }

        return searchResultStrings;
    }
}