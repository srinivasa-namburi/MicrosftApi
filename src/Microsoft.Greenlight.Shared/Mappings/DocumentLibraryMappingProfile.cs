using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;

namespace Microsoft.Greenlight.Shared.Mappings;

public class DocumentLibraryMappingProfile : Profile
{
    public DocumentLibraryMappingProfile()
    {
        CreateMap<DocumentLibrary, DocumentLibraryInfo>()
            .ReverseMap();

        CreateMap<DocumentLibraryDocumentProcessAssociation, DocumentLibraryDocumentProcessAssociationInfo>()
            .ForMember(dest => dest.DocumentProcessShortName, opt => opt.MapFrom(src => src.DynamicDocumentProcessDefinition.ShortName))
            .ReverseMap();

        CreateMap<DocumentLibraryInfo, DocumentLibraryUsageInfo>()
            .ForMember(dest=>dest.DocumentLibraryShortName, opt=>opt.MapFrom(src=>src.ShortName));
        
        CreateMap<DocumentLibrary, DocumentLibraryUsageInfo>()
            .ForMember(dest=>dest.DocumentLibraryShortName, opt=>opt.MapFrom(src=>src.ShortName));
    }
}