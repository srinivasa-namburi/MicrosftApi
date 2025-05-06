using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.Plugins;
using System;

namespace Microsoft.Greenlight.Shared.Mappings
{
    /// <summary>
    /// Profile for mapping plugin-related entities to their corresponding DTOs.
    /// </summary>
    public class PluginMappingProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginMappingProfile"/> class.
        /// Defines the mapping between plugin entities and their DTOs.
        /// </summary>
        public PluginMappingProfile()
        {
            // MCP plugin mappings
            CreateMap<McpPlugin, McpPluginInfo>()
                .ForMember(dest => dest.LatestVersion, opt => opt.MapFrom(src => src.LatestVersion))
                .ForMember(dest => dest.SourceType, opt => opt.MapFrom(src => src.SourceType.ToString()))
                .ReverseMap()
                .ForMember(dest => dest.SourceType, opt => 
                    opt.MapFrom(src => Enum.Parse<McpPluginSourceType>(src.SourceType)));

            CreateMap<McpPluginVersion, McpPluginVersionInfo>()
                .ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.Url))
                .ForMember(dest => dest.AuthenticationType, opt => opt.MapFrom(src => src.AuthenticationType))
                .ReverseMap()
                .ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.Url))
                .ForMember(dest => dest.AuthenticationType, opt => opt.MapFrom(src => src.AuthenticationType));

            CreateMap<McpPluginDocumentProcess, McpPluginDocumentProcessInfo>()
                .ForMember(dest => dest.DocumentProcess, opt => opt.MapFrom(src => src.DynamicDocumentProcessDefinition))
                .ForMember(dest => dest.Plugin, opt => opt.MapFrom(src => src.McpPlugin))
                .ReverseMap()
                .ForMember(dest => dest.DynamicDocumentProcessDefinition, opt => opt.MapFrom(src => src.DocumentProcess))
                .ForMember(dest => dest.McpPlugin, opt => opt.MapFrom(src => src.Plugin));
        }
    }
}
