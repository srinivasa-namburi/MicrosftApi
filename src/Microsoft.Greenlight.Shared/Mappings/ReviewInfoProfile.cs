using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.Shared.Mappings;

public class ReviewInfoProfile : Profile
{
    public ReviewInfoProfile()
    {
        CreateMap<ReviewDefinition, ReviewDefinitionInfo>();
        CreateMap<ReviewDefinitionInfo, ReviewDefinition>();

        CreateMap<ReviewQuestion, ReviewQuestionInfo>();
        CreateMap<ReviewQuestionInfo, ReviewQuestion>();
    }
}
