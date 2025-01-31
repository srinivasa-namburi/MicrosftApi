using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Mappings
{
    /// <summary>
    /// Profile for mapping between <see cref="DynamicDocumentProcessMetaDataField"/> 
    /// and <see cref="DocumentProcessMetadataFieldInfo"/>.
    /// </summary>
    public class DocumentProcessMetadataFieldProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentProcessMetadataFieldProfile"/> class.
        /// Defines the mapping between <see cref="DynamicDocumentProcessMetaDataField"/> 
        /// and <see cref="DocumentProcessMetadataFieldInfo"/>.
        /// </summary>
        public DocumentProcessMetadataFieldProfile()
        {
            CreateMap<DynamicDocumentProcessMetaDataField, DocumentProcessMetadataFieldInfo>()
                .ReverseMap();
        }
    }
}