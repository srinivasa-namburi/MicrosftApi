using AutoMapper;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using System.Linq.Expressions;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping document process information.
/// </summary>
public class DocumentProcessInfoProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentProcessInfoProfile"/> class.
    /// Defines the mapping between <see cref="DocumentProcessOptions"/> and <see cref="DocumentProcessInfo"/>, 
    /// and between <see cref="DynamicDocumentProcessDefinition"/> and <see cref="DocumentProcessInfo"/>.
    /// </summary>
    public DocumentProcessInfoProfile()
    {

        CreateMap<DocumentProcessOptions, DocumentProcessInfo>()
            // For the Id property, set it to Guid.Empty as this isn't used in static document process definitions
            .ForMember(x => x.Id, y => y.MapFrom(source => Guid.Empty))
            .ForMember(x => x.ShortName, y => y.MapFrom(source => source.Name))
            .ForMember(x => x.Description, y => y.MapFrom(source => ""))
            .ForMember(x => x.OutlineText, y => y.MapFrom(source => ""))
            .ForMember(x => x.Repositories, y => y.MapFrom(source => source.Repositories))
            .ForMember(x => x.PrecedingSearchPartitionInclusionCount, y => y.Ignore())
            .ForMember(x => x.FollowingSearchPartitionInclusionCount, y => y.Ignore())
            .ForMember(x => x.NumberOfCitationsToGetFromRepository, y => y.Ignore())
            .ForMember(x => x.MinimumRelevanceForCitations, y => y.Ignore())
            .ForMember(x => x.CompletionServiceType, y => y.Ignore())
            .ForMember(x => x.ValidationPipelineId, y => y.Ignore())
            .ForMember(x => x.AiModelDeploymentId, y => y.MapFrom(source => Guid.Empty))
            .ForMember(x => x.AiModelDeploymentForValidationId, y => y.MapFrom(source => Guid.Empty))
            .ForMember(x => x.LogicType, y => y.MapFrom(
                source => Enum.Parse<DocumentProcessLogicType>(source.IngestionMethod ?? "KernelMemory")));


        CreateMap<DocumentProcessInfo, DynamicDocumentProcessDefinition>()
            .ForMember(x => x.LogicType, y => y.MapFrom(source => source.LogicType.ToString()))
            .ForMember(x => x.CompletionServiceType, y => y.MapFrom(
                source => source.CompletionServiceType ?? DocumentProcessCompletionServiceType.GenericAiCompletionService))
            .ForMember(dest => dest.Repositories, opt => opt.MapFrom(src => src.Repositories))
            .ForMember(x => x.McpServerAssociations, y => y.DoNotUseDestinationValue());

        CreateMap<DynamicDocumentProcessDefinition, DocumentProcessInfo>()
            .ForMember(dest => dest.Repositories, opt => opt.MapFrom(src => src.Repositories))
            .ForMember(x => x.OutlineText, y => y.MapFrom(MapDocumentOutline()))
            .ForMember(x => x.DocumentOutlineId, MapDocumentOutlineId);
    }

    /// <summary>
    /// Maps the document outline to its full text if it exists, otherwise returns an empty string.
    /// </summary>
    /// <returns>An expression that maps the document outline to its full text or an empty string.</returns>
    private static Expression<Func<DynamicDocumentProcessDefinition, string>> MapDocumentOutline()
    {
        return source => source.DocumentOutline != null ? source.DocumentOutline.FullText : string.Empty;
    }

    /// <summary>
    /// Maps the document outline ID to the destination object. If the source DocumentOutlineId is null,
    /// it maps the ID of the mapped DocumentOutline.Id if it exists, otherwise maps to Guid.Empty.
    /// </summary>
    /// <param name="obj">The member configuration expression to configure the mapping.</param>
    private void MapDocumentOutlineId(
        IMemberConfigurationExpression<DynamicDocumentProcessDefinition, DocumentProcessInfo, Guid?> obj)
    {
        obj.MapFrom((source, dest) => source.DocumentOutlineId ?? source.DocumentOutline?.Id ?? Guid.Empty);
    }
}
