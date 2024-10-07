using AutoMapper;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Mappings;

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
            .ForMember(x => x.Repositories, y => y.MapFrom(source => source.Repositories))
            .ForMember(x => x.LogicType,
                y => y.MapFrom(source =>
                    Enum.Parse<DocumentProcessLogicType>(source.IngestionMethod ?? "KernelMemory")));

        CreateMap<DocumentProcessInfo, DynamicDocumentProcessDefinition>()
            .ForMember(x => x.LogicType, y => y.MapFrom(source => source.LogicType.ToString()))
            .ForMember(x => x.Plugins, y => y.DoNotUseDestinationValue());

        CreateMap<DynamicDocumentProcessDefinition, DocumentProcessInfo>()
            .ForMember(x => x.OutlineText, y => y.MapFrom(source => source.DocumentOutline!.FullText ?? string.Empty))
            .ForMember(x => x.DocumentOutlineId, DocumentOutlineIdCheck);

    }

    private void DocumentOutlineIdCheck(IMemberConfigurationExpression<DynamicDocumentProcessDefinition, DocumentProcessInfo, Guid?> obj)
    {
        // If the DocumentOutlineId is null, if it is, check if DocumentOutline.Id is null, if it is, set it to Guid.Empty
        obj.MapFrom((source, dest) => source.DocumentOutlineId ?? source.DocumentOutline?.Id ?? Guid.Empty);
    }
}
