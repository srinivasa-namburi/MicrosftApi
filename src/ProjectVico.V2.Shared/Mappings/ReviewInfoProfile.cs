using AutoMapper;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models.Review;

namespace ProjectVico.V2.Shared.Mappings;

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