using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;
using Microsoft.Greenlight.Shared.Models.Configuration;

namespace Microsoft.Greenlight.Shared.Mappings
{
    /// <summary>
    /// Profile for mapping between <see cref="AiModelDeploymentInfo"/> and <see cref="AiModelDeployment"/>.
    /// </summary>
    public class AiModelProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiModelProfile"/> class.
        /// Defining the mapping between AiModelDeploymentInfo and AiModelDeployment.
        /// </summary>
        public AiModelProfile()
        {
            CreateMap<AiModelDeployment, AiModelDeploymentInfo>()
                .ForMember(x => x.AiModel, y => y.MapFrom(source => source.AiModel))
                .ForMember(x => x.EmbeddingSettings, y => y.MapFrom(s => s.EmbeddingSettings));
            
            CreateMap<AiModelDeploymentInfo, AiModelDeployment>()
               .ForMember(x => x.AiModel, y => y.Ignore())
               .ForMember(x => x.EmbeddingSettings, y => y.MapFrom(s => s.EmbeddingSettings));

            CreateMap<AiModel, AiModelInfo>()
                .ForMember(x => x.ModelType, y => y.MapFrom(s => s.ModelType))
                .ForMember(x => x.EmbeddingSettings, y => y.MapFrom(s => s.EmbeddingSettings))
                .ReverseMap();
        }
    }
}