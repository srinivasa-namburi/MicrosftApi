using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing.Search;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing.Generation;

public class USNuclearLicensingBodyTextGenerator : IBodyTextGenerator
{
    private readonly IAiCompletionService _aiCompletionService;
    private readonly IUSNuclearLicensingRagRepository _ragRepository;
    private readonly ILogger<USNuclearLicensingBodyTextGenerator> _logger;

    public USNuclearLicensingBodyTextGenerator(
        [FromKeyedServices("US.NuclearLicensing-IAiCompletionService")]
        IAiCompletionService aiCompletionService,
        ILogger<USNuclearLicensingBodyTextGenerator> logger,
        IUSNuclearLicensingRagRepository ragRepository)
    {
        _aiCompletionService = aiCompletionService;
        _logger = logger;
        _ragRepository = ragRepository;
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
        if (contentNodeType == ContentNodeType.Title)
        {
            documents = await _ragRepository.SearchOnlyTitlesAsync(sectionOrTitleNumber + " " + sectionOrTitleText);

        }
        else if (contentNodeType == ContentNodeType.Heading)
        {
            documents = await _ragRepository.SearchOnlySubsectionsAsync(sectionOrTitleNumber + " " + sectionOrTitleText);

        }

        return documents;
    }
}
