using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    
    /// <summary>
    /// Factory for resolving the appropriate content reference generation service based on content type
    /// </summary>
    public interface IContentReferenceGenerationServiceFactory
    {
        /// <summary>
        /// Gets the appropriate content reference generation service for the specified content type
        /// </summary>
        /// <param name="referenceType">The type of content reference</param>
        /// <returns>The content reference generation service, or null if not found</returns>
        object? GetGenerationService(ContentReferenceType referenceType);
        
        /// <summary>
        /// Gets the appropriate strongly-typed content reference generation service for the specified content type
        /// </summary>
        /// <typeparam name="T">The type of content</typeparam>
        /// <param name="referenceType">The type of content reference</param>
        /// <returns>The content reference generation service, or null if not found</returns>
        IContentReferenceGenerationService<T>? GetGenerationService<T>(ContentReferenceType referenceType) where T : EntityBase;
    }
}

