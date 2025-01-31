using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Mappings
{
    /// <summary>
    /// Profile for mapping plugin-related entities to their corresponding DTOs.
    /// </summary>
    public class PluginMappingProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginMappingProfile"/> class.
        /// Defines the mapping between <see cref="DynamicPlugin"/> and <see cref="DynamicPluginInfo"/>,
        /// between <see cref="DynamicPluginVersion"/> and <see cref="DynamicPluginVersionInfo"/>,
        /// and between <see cref="DynamicPluginDocumentProcess"/> and <see cref="DynamicPluginDocumentProcessInfo"/>.
        /// </summary>
        public PluginMappingProfile()
        {
            CreateMap<DynamicPlugin, DynamicPluginInfo>()
                .ForMember(dest => dest.LatestVersion, opt => opt.MapFrom(src => src.LatestVersion))
                .ReverseMap();

            CreateMap<DynamicPluginVersion, DynamicPluginVersionInfo>()
                .ReverseMap();

            CreateMap<DynamicPluginDocumentProcess, DynamicPluginDocumentProcessInfo>()
                .ReverseMap();
        }
    }
}
