using AutoMapper;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models.DocumentProcess;

namespace ProjectVico.V2.Shared.Mappings;

public class DocumentProcessInfoProfile : Profile
{
    public DocumentProcessInfoProfile()
    {
        CreateMap<DocumentProcessOptions, DocumentProcessInfo>()
            // For the Id property, set it to Guid.Empty as this isn't used in static document process definitions
            .ForMember(x => x.Id, y => y.MapFrom(source => Guid.Empty))
            .ForMember(x => x.ShortName, y => y.MapFrom(source => source.Name))
            .ForMember(x => x.Description, y => y.MapFrom(source => ""))
            .ForMember(x => x.OutlineText, y => y.MapFrom(source => ""))
            .ForMember(x => x.LogicType,
                y => y.MapFrom(source =>
                    Enum.Parse<DocumentProcessLogicType>(source.IngestionMethod ?? "KernelMemory")));

        CreateMap<DocumentProcessInfo, DynamicDocumentProcessDefinition>()
            .ForMember(x => x.LogicType, y => y.MapFrom(source => source.LogicType.ToString()));

        CreateMap<DynamicDocumentProcessDefinition, DocumentProcessInfo>()
            .ForMember(x => x.OutlineText, y => y.MapFrom(source => source.DocumentOutline!.FullText ?? string.Empty))
            .ForMember(x => x.DocumentOutlineId, y => y.MapFrom(source => source.DocumentOutline.Id));
    }
}