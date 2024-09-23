using AutoMapper;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Mappings;

public class ExportedDocumentLinkProfile : Profile
{
    public ExportedDocumentLinkProfile()
    {
        CreateMap<ExportedDocumentLink, ExportedDocumentLinkInfo>()
            .ForMember(x=>x.GeneratedDocumentId, map=>map.MapFrom(
                src=>src.GeneratedDocument != null ? 
                    src.GeneratedDocument.Id : 
                    src.GeneratedDocumentId));

        CreateMap<ExportedDocumentLinkInfo, ExportedDocumentLink>();
    }
}