using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.DocumentProcess.Dynamic;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;

public class KernelMemoryBodyTextGenerator : IBodyTextGenerator
{
    private readonly IConsolidatedSearchOptionsFactory _searchOptionsFactory;
    private readonly DynamicDocumentProcessServiceFactory _dpServiceFactory;
    private IKernelMemoryRepository? _kernelMemoryRepository;
    private string _documentProcessName;
    private readonly ILogger<KernelMemoryBodyTextGenerator> _logger;
    private readonly IServiceProvider _sp;

    public KernelMemoryBodyTextGenerator(
        IConsolidatedSearchOptionsFactory searchOptionsFactory,
        DynamicDocumentProcessServiceFactory dpServiceFactory,
        ILogger<KernelMemoryBodyTextGenerator> logger,
        IServiceProvider sp)
    {
        _searchOptionsFactory = searchOptionsFactory;
        _dpServiceFactory = dpServiceFactory;
        _logger = logger;
        _sp = sp;
    }

    public async Task<List<ContentNode>> GenerateBodyText(string contentNodeTypeString, string sectionNumber,
        string sectionTitle, string tableOfContentsString, string documentProcessName, Guid? metadataId,
        ContentNode? sectionContentNode)
    {
        _documentProcessName = documentProcessName;
        _kernelMemoryRepository = _dpServiceFactory.GetService<IKernelMemoryRepository>(_documentProcessName);
        var aiCompletionService = _dpServiceFactory.GetService<IAiCompletionService>(_documentProcessName);

        if (_kernelMemoryRepository == null)
        {
            _logger.LogError("KernelMemoryBodyTextGenerator: Kernel memory repository is null.");
            throw new InvalidOperationException("Kernel memory repository is not available.");
        }

        var contentNodeType = Enum.Parse<ContentNodeType>(contentNodeTypeString);
        var documents = await GetDocumentsForQuery(contentNodeType, sectionNumber, sectionTitle);

        return await aiCompletionService!.GetBodyContentNodes(
            documents,
            sectionNumber,
            sectionTitle,
            contentNodeType,
            tableOfContentsString,
            metadataId, 
            sectionContentNode);
    }

    private async Task<List<DocumentProcessRepositorySourceReferenceItem>> GetDocumentsForQuery(ContentNodeType contentNodeType, string sectionOrTitleNumber,
        string sectionOrTitleText)
    {
        var searchOptions =
            await _searchOptionsFactory.CreateSearchOptionsForDocumentProcessAsync(_documentProcessName);

        var resultItems = await _kernelMemoryRepository!.SearchAsync(
            _documentProcessName, sectionOrTitleNumber + " " + sectionOrTitleText, searchOptions);

        // The results are of type SourceReferenceItem, but we need to cast them to DocumentProcessKnowledgeRepositorySourceReferenceItem
        var documents = resultItems.Select(x => (DocumentProcessRepositorySourceReferenceItem)x).ToList();
        return documents;
    }
}
