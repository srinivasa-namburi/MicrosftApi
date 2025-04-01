using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Services.ContentReference;

/// <summary>
/// Service responsible for generating content reference data for specific content types.
/// </summary>
/// <typeparam name="T">The type of content to generate references for</typeparam>
public interface IContentReferenceGenerationService<in T> where T : EntityBase
{
    /// <summary>
    /// Generates content reference items from the specified source content.
    /// </summary>
    /// <param name="source">The source content to generate references from</param>
    /// <returns>A list of content reference items</returns>
    Task<List<ContentReferenceItemInfo>> GenerateReferencesAsync(T source);
    
    /// <summary>
    /// Generates a text representation for RAG usage from the specified content.
    /// </summary>
    /// <param name="contentId">The ID of the content to generate text for</param>
    /// <returns>A string representation optimized for RAG usage</returns>
    Task<string?> GenerateContentTextForRagAsync(Guid contentId);
    
    /// <summary>
    /// Generates vector embeddings for the specified content.
    /// </summary>
    /// <param name="contentId">The ID of the content to generate embeddings for</param>
    /// <returns>An array of floating point values representing the content embedding</returns>
    Task<float[]> GenerateEmbeddingsAsync(Guid contentId);
}