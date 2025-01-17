using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Services.Search
{
    public class ConsolidatedSearchOptionsFactory : IConsolidatedSearchOptionsFactory
    {
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly ConsolidatedSearchOptions _defaultOptions;

        public ConsolidatedSearchOptionsFactory(IDocumentProcessInfoService documentProcessInfoService)
        {
            _documentProcessInfoService = documentProcessInfoService;
            _defaultOptions = new ConsolidatedSearchOptions
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                IndexName = "default",
                Top = 5,
                MinRelevance = 0.7,
                PrecedingPartitionCount = 0,
                FollowingPartitionCount = 0
            };
        }

        public async Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentProcessAsync(string documentProcessName)
        {
            var documentProcess =
                await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
            return await CreateSearchOptionsForDocumentProcessAsync(documentProcess);
        }

        public async Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentProcessAsync(DocumentProcessInfo documentProcess)
        {
            var options = new ConsolidatedSearchOptions
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                IndexName = documentProcess.Repositories.FirstOrDefault() ?? "default",
                Top = documentProcess.NumberOfCitationsToGetFromRepository,
                MinRelevance = documentProcess.MinimumRelevanceForCitations,
                PrecedingPartitionCount = documentProcess.PrecedingSearchPartitionInclusionCount,
                FollowingPartitionCount = documentProcess.FollowingSearchPartitionInclusionCount
            };

            if (options.Top == 0)
            {
                options.Top = _defaultOptions.Top;
            }
            if (options.MinRelevance == 0)
            {
                options.MinRelevance = _defaultOptions.MinRelevance;
            }

            return options;
        }

        public async Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentLibraryAsync(DocumentLibraryInfo documentLibrary)
        {
            var options = new ConsolidatedSearchOptions
            {
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
                IndexName = documentLibrary.IndexName,
                Top = _defaultOptions.Top,
                MinRelevance = _defaultOptions.MinRelevance,
                PrecedingPartitionCount = 0,
                FollowingPartitionCount = 0
            };

            return options;
        }

        public async Task<ConsolidatedSearchOptions> CreateSearchOptionsForReviewsAsync(Dictionary<string,string> tags)
        {
            var options = new ConsolidatedSearchOptions()
            {
                DocumentLibraryType = DocumentLibraryType.Reviews,
                IndexName = "index-reviews",
                Top = _defaultOptions.Top,
                MinRelevance = _defaultOptions.MinRelevance,
                PrecedingPartitionCount = 0,
                FollowingPartitionCount = 0,
                ParametersExactMatch = tags
            };

            return options;
        }
    }
}