using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Mappings;

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
