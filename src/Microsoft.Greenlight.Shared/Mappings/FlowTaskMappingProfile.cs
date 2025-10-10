// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Microsoft.Greenlight.Shared.Models.FlowTasks;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// AutoMapper profile for Flow Task entities and DTOs.
/// </summary>
public class FlowTaskMappingProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskMappingProfile"/> class.
    /// </summary>
    public FlowTaskMappingProfile()
    {
        // Summary DTO mappings (existing)
        CreateMap<FlowTaskTemplate, FlowTaskTemplateInfo>()
            .ForMember(dest => dest.SectionCount,
                opt => opt.MapFrom(src => src.Sections != null ? src.Sections.Count : 0))
            .ForMember(dest => dest.TotalRequirementCount,
                opt => opt.MapFrom(src => src.Sections != null
                    ? src.Sections.Sum(s => s.Requirements != null ? s.Requirements.Count : 0)
                    : 0));

        CreateMap<FlowTaskTemplateInfo, FlowTaskTemplate>()
            .ForMember(dest => dest.Sections, opt => opt.Ignore())
            .ForMember(dest => dest.OutputTemplates, opt => opt.Ignore())
            .ForMember(dest => dest.DataSources, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedUtc, opt => opt.Ignore());

        // Detailed DTO mappings (new for UI)
        CreateMap<FlowTaskTemplate, FlowTaskTemplateDetailDto>().ReverseMap()
            .ForMember(dest => dest.CreatedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        CreateMap<FlowTaskSection, FlowTaskSectionDto>().ReverseMap()
            .ForMember(dest => dest.FlowTaskTemplate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        CreateMap<FlowTaskRequirement, FlowTaskRequirementDto>().ReverseMap()
            .ForMember(dest => dest.FlowTaskSection, opt => opt.Ignore())
            .ForMember(dest => dest.DataSource, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        CreateMap<FlowTaskOutputTemplate, FlowTaskOutputTemplateDto>().ReverseMap()
            .ForMember(dest => dest.FlowTaskTemplate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        // Data source mappings with TPH inheritance
        CreateMap<FlowTaskDataSource, FlowTaskDataSourceDto>()
            .Include<FlowTaskMcpToolDataSource, FlowTaskMcpToolDataSourceDto>()
            .Include<FlowTaskStaticDataSource, FlowTaskStaticDataSourceDto>();

        CreateMap<FlowTaskDataSourceDto, FlowTaskDataSource>()
            .Include<FlowTaskMcpToolDataSourceDto, FlowTaskMcpToolDataSource>()
            .Include<FlowTaskStaticDataSourceDto, FlowTaskStaticDataSource>()
            .ForMember(dest => dest.FlowTaskTemplate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        CreateMap<FlowTaskMcpToolDataSource, FlowTaskMcpToolDataSourceDto>().ReverseMap()
            .ForMember(dest => dest.McpPlugin, opt => opt.Ignore())
            .ForMember(dest => dest.FlowTaskTemplate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        CreateMap<FlowTaskStaticDataSource, FlowTaskStaticDataSourceDto>().ReverseMap()
            .ForMember(dest => dest.FlowTaskTemplate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        CreateMap<FlowTaskMcpToolParameter, FlowTaskMcpToolParameterDto>().ReverseMap()
            .ForMember(dest => dest.FlowTaskMcpToolDataSource, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedUtc, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());
    }
}
