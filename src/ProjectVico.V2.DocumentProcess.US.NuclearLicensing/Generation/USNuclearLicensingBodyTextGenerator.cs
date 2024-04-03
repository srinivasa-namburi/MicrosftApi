using Microsoft.Extensions.Logging;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.Plugins.Default.NuclearDocs.Services.AiCompletionService;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Generation;

public class USNuclearLicensingBodyTextGenerator : IBodyTextGenerator
{
    private readonly IAiCompletionService _aiCompletionService;
    private readonly IIndexingProcessor _indexingProcessor;
    private readonly ILogger<USNuclearLicensingBodyTextGenerator> _logger;

    public USNuclearLicensingBodyTextGenerator(
        IAiCompletionService aiCompletionService, 
        IIndexingProcessor indexingProcessor,
        ILogger<USNuclearLicensingBodyTextGenerator> logger)
    {
        _aiCompletionService = aiCompletionService;
        _indexingProcessor = indexingProcessor;
        _logger = logger;
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
            Enum.Parse<ContentNodeType>(contentNodeTypeString),
            tableOfContentsString,
            metadataId
        );
    }

    private async Task<List<ReportDocument>> GetDocumentsForQuery(ContentNodeType contentNodeType, string sectionOrTitleNumber,
        string sectionOrTitleText)
    {
        List<ReportDocument> documents = new List<ReportDocument>();
        if (contentNodeType == ContentNodeType.Heading)
        {
            documents = await _indexingProcessor.SearchWithHybridSearch(sectionOrTitleNumber + " " +
                                                                        sectionOrTitleText);
        }
        else if (contentNodeType == ContentNodeType.Title)
        {

            documents = await _indexingProcessor.SearchWithTitleSearch(sectionOrTitleNumber + " " +
                                                                       sectionOrTitleText);
        }

        return documents;
    }
}