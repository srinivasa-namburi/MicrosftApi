using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

namespace Microsoft.Greenlight.Shared.Services.Search
{
    /// <summary>
    /// Factory interface for creating consolidated search options.
    /// </summary>
    public interface IConsolidatedSearchOptionsFactory
    {
        /// <summary>
        /// Creates search options for a document process by name.
        /// </summary>
        /// <param name="documentProcessName">The name of the document process.</param>
        /// <returns>The consolidated search options.</returns>
        Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentProcessAsync(string documentProcessName);

        /// <summary>
        /// Creates search options for a document process.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>The consolidated search options.</returns>
        Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentProcessAsync(DocumentProcessInfo documentProcess);

        /// <summary>
        /// Creates search options for a document library.
        /// </summary>
        /// <param name="documentLibrary">The document library information.</param>
        /// <returns>The consolidated search options.</returns>
        Task<ConsolidatedSearchOptions> CreateSearchOptionsForDocumentLibraryAsync(DocumentLibraryInfo documentLibrary);

        /// <summary>
        /// Creates search options for reviews based on tags.
        /// </summary>
        /// <param name="tags">The tags to filter reviews.</param>
        /// <returns>The consolidated search options.</returns>
        Task<ConsolidatedSearchOptions> CreateSearchOptionsForReviewsAsync(Dictionary<string, string> tags);
    }
}