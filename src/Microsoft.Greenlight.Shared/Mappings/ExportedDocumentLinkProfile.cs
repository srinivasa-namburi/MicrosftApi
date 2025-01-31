using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping between <see cref="ExportedDocumentLink"/> and <see cref="ExportedDocumentLinkInfo"/>.
/// </summary>
public class ExportedDocumentLinkProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportedDocumentLinkProfile"/> class.
    /// Defines the mapping between <see cref="ExportedDocumentLink"/> and <see cref="ExportedDocumentLinkInfo"/>.
    /// </summary>
    public ExportedDocumentLinkProfile()
    {
        CreateMap<ExportedDocumentLink, ExportedDocumentLinkInfo>()
            .ForMember(x => x.GeneratedDocumentId, map => map.MapFrom(
                src => src.GeneratedDocument != null ?
                    src.GeneratedDocument.Id :
                    src.GeneratedDocumentId));

        CreateMap<ExportedDocumentLinkInfo, ExportedDocumentLink>();
    }
}
