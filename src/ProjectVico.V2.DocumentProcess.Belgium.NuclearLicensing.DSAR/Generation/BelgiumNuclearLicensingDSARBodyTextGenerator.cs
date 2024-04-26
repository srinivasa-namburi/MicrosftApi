using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Belgium.NuclearLicensing.DSAR.Generation;

public class BelgiumNuclearLicensingDSARBodyTextGenerator : IBodyTextGenerator
{
    private const string ProcessName = "Belgium.NuclearLicensing.DSAR";
    private readonly IAiCompletionService _aiCompletionService;
    private readonly IKernelMemoryRepository _kernelMemoryRepository;
    private readonly ILogger<BelgiumNuclearLicensingDSARBodyTextGenerator> _logger;

    public BelgiumNuclearLicensingDSARBodyTextGenerator
    (
        [FromKeyedServices(ProcessName+"-IAiCompletionService")]
        IAiCompletionService aiCompletionService,
        [FromKeyedServices(ProcessName+"-IKernelMemoryRepository")]
        IKernelMemoryRepository kernelMemoryRepository,
        ILogger<BelgiumNuclearLicensingDSARBodyTextGenerator> logger 
    )
    {
        _aiCompletionService = aiCompletionService;
        _logger = logger;
        _kernelMemoryRepository = kernelMemoryRepository;
    }

    public async Task<List<ContentNode>> GenerateBodyText(string contentNodeTypeString, string sectionNumber,
        string sectionTitle, string tableOfContentsString, Guid? metadataId)
    {
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
       
        var searchResult = await _kernelMemoryRepository.SearchAsync(ProcessName, sectionOrTitleNumber + " " + sectionOrTitleText);

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