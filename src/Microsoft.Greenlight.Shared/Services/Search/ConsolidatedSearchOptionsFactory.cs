using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Services.Search
{
    /// <summary>
    /// Factory class for creating consolidated search options.
    /// </summary>
    public class ConsolidatedSearchOptionsFactory : IConsolidatedSearchOptionsFactory
    {
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly ConsolidatedSearchOptions _defaultOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsolidatedSearchOptionsFactory"/> class.
        /// </summary>
        /// <param name="documentProcessInfoService">The document process info service.</param>
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
                FollowingPartitionCount = 0,
                EnableProgressiveSearch = true
            };
        }

        /// <inheritdoc/>
        public async Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentProcessAsync(string documentProcessName)
        {
            var documentProcess =
                await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);

            if (documentProcess == null)
            {
                return _defaultOptions;
            }

            return await CreateSearchOptionsForDocumentProcessAsync(documentProcess);
        }

        /// <inheritdoc/>
        public async Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentProcessAsync(DocumentProcessInfo documentProcess)
        {
            var options = new ConsolidatedSearchOptions
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                IndexName = documentProcess.Repositories.FirstOrDefault() ?? "default",
                Top = documentProcess.NumberOfCitationsToGetFromRepository,
                MinRelevance = documentProcess.MinimumRelevanceForCitations,
                PrecedingPartitionCount = documentProcess.PrecedingSearchPartitionInclusionCount,
                FollowingPartitionCount = documentProcess.FollowingSearchPartitionInclusionCount,
                EnableProgressiveSearch = true
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

        /// <inheritdoc/>
        public async Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentLibraryAsync(DocumentLibraryInfo documentLibrary)
        {
            var options = new ConsolidatedSearchOptions
            {
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
                IndexName = documentLibrary.IndexName,
                Top = _defaultOptions.Top,
                MinRelevance = _defaultOptions.MinRelevance,
                PrecedingPartitionCount = 0,
                FollowingPartitionCount = 0,
                EnableProgressiveSearch = true
            };

            return options;
        }

        /// <inheritdoc/>
        public async Task<ConsolidatedSearchOptions> CreateSearchOptionsForReviewsAsync(Dictionary<string, string> tags)
        {
            var options = new ConsolidatedSearchOptions()
            {
                DocumentLibraryType = DocumentLibraryType.Reviews,
                IndexName = "index-reviews",
                Top = _defaultOptions.Top,
                MinRelevance = _defaultOptions.MinRelevance,
                PrecedingPartitionCount = 0,
                FollowingPartitionCount = 0,
                ParametersExactMatch = tags,
                EnableProgressiveSearch = false
            };

            return options;
        }
    }
}