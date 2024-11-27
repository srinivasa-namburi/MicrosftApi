using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Mappings
{
    public class PluginMappingProfile : Profile
    {
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
