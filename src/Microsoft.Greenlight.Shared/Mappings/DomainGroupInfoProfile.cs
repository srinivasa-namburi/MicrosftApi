using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.DomainGroups;

namespace Microsoft.Greenlight.Shared.Mappings
{
    /// <summary>
    /// Profile for mapping domain group information.
    /// </summary>
    public class DomainGroupInfoProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DomainGroupInfoProfile"/> class.
        /// Defines the mapping between <see cref="DomainGroup"/> and <see cref="DomainGroupInfo"/>.
        /// </summary>
        public DomainGroupInfoProfile()
        {
            CreateMap<DomainGroup, DomainGroupInfo>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.ExposeCoPilotAgentEndpoint, opt => opt.MapFrom(src => src.ExposeCoPilotAgentEndpoint))
                .ForMember(dest => dest.AuthenticateCoPilotAgentEndpoint, opt => opt.MapFrom(src => src.AuthenticateCoPilotAgentEndpoint))
                .ForMember(dest => dest.DocumentProcesses, opt => opt.MapFrom(src => src.DocumentProcesses))
                .ReverseMap();
                
        }
    }
}