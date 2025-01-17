using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models.SourceReferences;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Plugins.KmDocs;

public class KmDocsPlugin : IPluginImplementation
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DocumentProcessInfo _documentProcess;
    private readonly ConsolidatedSearchOptions _searchOptions;
    private IKernelMemory? _kernelMemory;
    private IKernelMemoryRepository? _kernelMemoryRepository;


    /// <summary>
    /// This class needs to be constructed per Document Process, which is handled in the paired PluginRegistration class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="documentProcess"></param>
    /// <param name="searchOptions"></param>
    public KmDocsPlugin(IServiceProvider serviceProvider, DocumentProcessInfo documentProcess, ConsolidatedSearchOptions searchOptions)
    {
        _serviceProvider = serviceProvider;
        _documentProcess = documentProcess;
        _searchOptions = searchOptions;

        Initialize();
    }

    private void Initialize()
    {
        _kernelMemory = _serviceProvider.GetKeyedService<IKernelMemory>(_documentProcess.ShortName + "-IKernelMemory");
        _kernelMemoryRepository =
            _serviceProvider.GetRequiredServiceForDocumentProcess<IKernelMemoryRepository>(_documentProcess);
    }

    [KernelFunction(nameof(AskQuestionAsync))]
    [Description("Ask a question to the underlying document process knowledge base")]
    public async Task<string> AskQuestionAsync(
        [Description("The question to ask the document repository. Make sure to format as a proper question ending with a question mark")]
        string question)
    {
        if (_kernelMemory == null)
        {
            throw new InvalidOperationException("Kernel Memory not found");
        }

        var dpRelevance = _documentProcess.MinimumRelevanceForCitations;

        var index = _documentProcess.Repositories[0];
        var response = await _kernelMemory.AskAsync(question, index: index, minRelevance: dpRelevance);
        var responseText = response.Result;

        return string.IsNullOrEmpty(responseText) ? "I'm sorry, I don't have an answer for that." : responseText;
    }

    [KernelFunction(nameof(SearchKnowledgeBase))]
    [Description("Search the underlying document process knowledge base. Prefer this over AskQuestionAsync.")]
    public async Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchKnowledgeBase(
        [Description("The search query to use")]
        string searchQuery)
    {
        if (_kernelMemoryRepository == null)
        {
            throw new InvalidOperationException("Kernel Memory Repository not found");
        }

        var searchResults =
            await _kernelMemoryRepository.SearchAsync(_documentProcess.ShortName, searchQuery, _searchOptions);

        return searchResults;
    }
}
