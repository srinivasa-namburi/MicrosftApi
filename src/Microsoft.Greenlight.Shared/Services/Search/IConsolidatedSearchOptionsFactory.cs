using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

namespace Microsoft.Greenlight.Shared.Services.Search
{
    public interface IConsolidatedSearchOptionsFactory
    {
        Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentProcessAsync(string documentProcessName);
        Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentProcessAsync(DocumentProcessInfo documentProcess);
        Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentLibraryAsync(DocumentLibraryInfo documentLibrary);
        Task<ConsolidatedSearchOptions> CreateSearchOptionsForReviewsAsync(Dictionary<string,string> tags);
        
    }
}