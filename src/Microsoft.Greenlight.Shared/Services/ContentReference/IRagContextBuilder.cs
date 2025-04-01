using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <summary>
    /// Interface for RAG context building service
    /// </summary>
    public interface IRagContextBuilder
    {
        /// <summary>
        /// Builds a context string with selected references for the user query
        /// </summary>
        Task<string> BuildContextWithSelectedReferencesAsync(
            string userQuery, 
            List<ContentReferenceItem> allReferences, 
            int topN = 5,
            int maxChunkTokens = 1200);
    }
}