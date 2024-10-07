using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation;

public class KernelMemoryBodyTextGenerator : IBodyTextGenerator
{
    private readonly IAiCompletionService _aiCompletionService;
    private IKernelMemoryRepository? _kernelMemoryRepository;
    private string _documentProcessName;
    private readonly ILogger<KernelMemoryBodyTextGenerator> _logger;
    private readonly IServiceProvider _sp;

    public KernelMemoryBodyTextGenerator(
        IAiCompletionService aiCompletionService,
        ILogger<KernelMemoryBodyTextGenerator> logger,
        IServiceProvider sp)
    {
        _aiCompletionService = aiCompletionService;
        _logger = logger;
        _sp = sp;
    }

    public async Task<List<ContentNode>> GenerateBodyText(string contentNodeTypeString, string sectionNumber,
        string sectionTitle, string tableOfContentsString, string documentProcessName, Guid? metadataId)
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
            metadataId
        );
    }

    private async Task<List<ReportDocument>> GetDocumentsForQuery(ContentNodeType contentNodeType, string sectionOrTitleNumber,
        string sectionOrTitleText)
    {
        List<ReportDocument> documents = new List<ReportDocument>();
       
        var searchResult = await _kernelMemoryRepository.SearchAsync(_documentProcessName, sectionOrTitleNumber + " " + sectionOrTitleText, top: 50);

        foreach (var resultDictionary in searchResult)
        {
            var document = new ReportDocument();
            foreach (var partition in resultDictionary.Values)
            {
                document.Content += partition.Text;
            }
            documents.Add(document);

        }
        
        return documents;
    }
}
