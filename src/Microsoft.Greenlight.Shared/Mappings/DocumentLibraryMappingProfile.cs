using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping between <see cref="DocumentLibrary"/> related models and DTOs.
/// </summary>
public class DocumentLibraryMappingProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentLibraryMappingProfile"/> class.
    /// Defines the mapping between <see cref="DocumentLibrary"/> and <see cref="DocumentLibraryInfo"/>, 
    /// between <see cref="DocumentLibraryDocumentProcessAssociation"/> and <see cref="DocumentLibraryDocumentProcessAssociationInfo"/>, 
    /// and between <see cref="DocumentLibraryInfo"/> and <see cref="DocumentLibraryUsageInfo"/>.
    /// </summary>
    public DocumentLibraryMappingProfile()
    {
        CreateMap<DocumentLibrary, DocumentLibraryInfo>()
            .ReverseMap();

        CreateMap<DocumentLibraryDocumentProcessAssociation, DocumentLibraryDocumentProcessAssociationInfo>()
            .ForMember(dest => dest.DocumentProcessShortName, opt => opt.MapFrom(src => src.DynamicDocumentProcessDefinition!.ShortName))
            .ReverseMap();

        CreateMap<DocumentLibraryInfo, DocumentLibraryUsageInfo>()
            .ForMember(dest => dest.DocumentLibraryShortName, opt => opt.MapFrom(src => src.ShortName));

        CreateMap<DocumentLibrary, DocumentLibraryUsageInfo>()
            .ForMember(dest => dest.DocumentLibraryShortName, opt => opt.MapFrom(src => src.ShortName));
    }
}
