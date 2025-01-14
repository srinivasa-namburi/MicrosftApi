using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Mappings
{
    public class DocumentProcessMetadataFieldProfile : Profile
    {
        public DocumentProcessMetadataFieldProfile()
        {
            CreateMap<DynamicDocumentProcessMetaDataField, DocumentProcessMetadataFieldInfo>()
                .ReverseMap();
        }
    }
}