using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation;

public class KernelMemoryBodyTextGenerator : IBodyTextGenerator
{
    private readonly IAiCompletionService _aiCompletionService;
    private readonly IConsolidatedSearchOptionsFactory _searchOptionsFactory;
    private IKernelMemoryRepository _kernelMemoryRepository;
    private string _documentProcessName;
    private readonly ILogger<KernelMemoryBodyTextGenerator> _logger;
    private readonly IServiceProvider _sp;

    public KernelMemoryBodyTextGenerator(
        IAiCompletionService aiCompletionService,
        IConsolidatedSearchOptionsFactory searchOptionsFactory,
        ILogger<KernelMemoryBodyTextGenerator> logger,
        IServiceProvider sp)
    {
        _aiCompletionService = aiCompletionService;
        _searchOptionsFactory = searchOptionsFactory;
        _logger = logger;
        _sp = sp;
    }

    public async Task<List<ContentNode>> GenerateBodyText(string contentNodeTypeString, string sectionNumber,
        string sectionTitle, string tableOfContentsString, string documentProcessName, Guid? metadataId,
        ContentNode? sectionContentNode)
    {
        _documentProcessName = documentProcessName;
        _kernelMemoryRepository = _sp.GetRequiredServiceForDocumentProcess<IKernelMemoryRepository>(_documentProcessName);
        
        var contentNodeType = Enum.Parse<ContentNodeType>(contentNodeTypeString);
        var documents = await GetDocumentsForQuery(contentNodeType, sectionNumber, sectionTitle);

        return await _aiCompletionService.GetBodyContentNodes(
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

        var resultItems = await _kernelMemoryRepository.SearchAsync(
            _documentProcessName, sectionOrTitleNumber + " " + sectionOrTitleText, searchOptions);

        // The results are of type SourceReferenceItem, but we need to cast them to DocumentProcessKnowledgeRepositorySourceReferenceItem
        var documents = resultItems.Select(x => (DocumentProcessRepositorySourceReferenceItem)x).ToList();
        return documents;
    }
}
