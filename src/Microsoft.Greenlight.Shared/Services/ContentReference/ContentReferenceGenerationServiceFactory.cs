using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <summary>
    /// Default implementation of IContentReferenceGenerationServiceFactory
    /// </summary>
    public class ContentReferenceGenerationServiceFactory : IContentReferenceGenerationServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        
        /// <summary>
        /// Creates a new instance of ContentReferenceGenerationServiceFactory
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve services</param>
        public ContentReferenceGenerationServiceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        
        /// <inheritdoc />
        public object? GetGenerationService(ContentReferenceType referenceType)
        {
            return referenceType switch
            {
                ContentReferenceType.GeneratedDocument => _serviceProvider.GetService<IContentReferenceGenerationService<GeneratedDocument>>(),
                ContentReferenceType.GeneratedSection => _serviceProvider.GetService<IContentReferenceGenerationService<ContentNode>>(),
                ContentReferenceType.ExternalFile => _serviceProvider.GetService<IContentReferenceGenerationService<ExportedDocumentLink>>(),
                ContentReferenceType.ReviewItem => _serviceProvider.GetService<IContentReferenceGenerationService<ReviewInstance>>(),
                // Add other content types as they are implemented
                _ => null
            };
        }

        /// <inheritdoc />
        public IContentReferenceGenerationService<T>? GetGenerationService<T>(ContentReferenceType referenceType) where T : EntityBase
        {
            var service = GetGenerationService(referenceType);
            return service as IContentReferenceGenerationService<T>;
        }
    }
}        //{
