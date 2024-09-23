using System.Text.Json;
using AutoMapper;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models.Review;

namespace ProjectVico.V2.Shared.Mappings;

public class ReviewInstanceInfoProfile : Profile
{
    public ReviewInstanceInfoProfile()
    {
        CreateMap<ReviewInstance, ReviewInstanceInfo>()
            .ForMember(dest => dest.ReviewDefinitionStateWhenSubmitted, 
                opt => opt.MapFrom(src => SerializeReviewDefinition(src.ReviewDefinition)));
        CreateMap<ReviewInstanceInfo, ReviewInstance>();
    }

    // Helper method to serialize the ReviewDefinition object to JSON
    private static string? SerializeReviewDefinition(ReviewDefinition? reviewDefinition)
    {
        return reviewDefinition != null ? JsonSerializer.Serialize(reviewDefinition) : null;
    }

    
    
}