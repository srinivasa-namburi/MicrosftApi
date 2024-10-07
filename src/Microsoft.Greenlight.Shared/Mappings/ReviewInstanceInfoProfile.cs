using System.Text.Json;
using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.Shared.Mappings;

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
