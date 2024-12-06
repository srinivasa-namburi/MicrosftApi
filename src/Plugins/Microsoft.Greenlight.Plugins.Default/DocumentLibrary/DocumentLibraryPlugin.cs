using System.ComponentModel;
using AutoMapper;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;

namespace Microsoft.Greenlight.Plugins.Default.DocumentLibrary;

public class DocumentLibraryPlugin : IPluginImplementation
{
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IAdditionalDocumentLibraryKernelMemoryRepository _kmRepository;
    private readonly IMapper _mapper;

    public DocumentLibraryPlugin(
        IDocumentLibraryInfoService documentLibraryInfoService,
        IDocumentProcessInfoService documentProcessInfoService,
        IAdditionalDocumentLibraryKernelMemoryRepository kmRepository,
        IMapper mapper
        )
    {
        _documentLibraryInfoService = documentLibraryInfoService;
        _documentProcessInfoService = documentProcessInfoService;
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
        
        var dp = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
        if (dp == null)
        {
            throw new Exception("For assistant : Wrong Document Process ShortName provided - no valid document process found.");
        }
        
        var documentLibrariesForDocumentProcess = await _documentLibraryInfoService.GetDocumentLibrariesByProcessIdAsync(dp.Id);

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

        var documentLibrary =
            await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);

        if (documentLibrary == null)
        {
            return "For assistant : Wrong Document Library ShortName provided - no valid library found. Please use the DocumentProcessShortName returned by GetDocumentLibraryInfo";
        }

        var memoryAnswer = await _kmRepository.AskAsync(documentLibraryShortName, indexName, questionText);
        if (memoryAnswer?.Result == null || memoryAnswer?.Result == "INFO NOT FOUND")
        {
            return "No answer found - please try another query or reformulate to use the SearchDocumentLibrary function instead";
        }

        var resultText = memoryAnswer!.Result;
        return resultText;
    }

    [KernelFunction("SearchDocumentLibrary")]
    [Description("Search the contents of a document library. Do not search without a valid documentLibraryShortName " +
                 "which you MUST retrieve from 'GetDocumentLibraryInfo'")]
    public async Task<List<DocumentLibrarySourceReferenceItem>> SearchDocumentLibraryAsync(
            [Description("The short name of the document library to search. " +
                         "You MUST retrieve these shortnames using the plugin method 'GetDocumentLibraryInfo' BEFORE calling this method. The libraries " +
                         "in that list are the ONLY valid libraries for this method. Don't call this method without the correct documentLibraryShortName." +
                         "DON'T use the index name - use only the documentLibraryShortName")]
            string documentLibraryShortName,
            [Description("The search text to use for the search")]
            string searchText,
            [Description("The number of results to return. Minimum value 12, may be up to 25")]
            int top = 12,
            [Description("The minimum relevance score for a result to be returned")]
            double minRelevance = 0.7
        )
    {

        var documentLibrary =
            await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryShortName);
        if (documentLibrary == null)
        {
            throw new Exception(
                "For assistant : Wrong Document Library ShortName provided - no valid library found. Please use the DocumentProcessShortName returned by GetDocumentLibraryInfo");
        }

        var searchResults = await _kmRepository.SearchAsync(documentLibraryShortName, searchText, top, minRelevance);

        var documentLibrarySourceReferenceItems = new List<DocumentLibrarySourceReferenceItem>();
        foreach (var sourceReferenceItem in searchResults)
        {
            if (sourceReferenceItem is DocumentLibrarySourceReferenceItem item)
            {
                documentLibrarySourceReferenceItems.Add(item);
            }
        }

        return documentLibrarySourceReferenceItems;
    }
}