using System.Text.Json;
using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping between <see cref="ReviewInstance"/> and <see cref="ReviewInstanceInfo"/>.
/// </summary>
public class ReviewInstanceInfoProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewInstanceInfoProfile"/> class.
    /// Defines the mapping between <see cref="ReviewInstance"/> and <see cref="ReviewInstanceInfo"/>.
    /// </summary>
    public ReviewInstanceInfoProfile()
    {
        CreateMap<ReviewInstance, ReviewInstanceInfo>()
            .ForMember(dest => dest.ReviewDefinitionStateWhenSubmitted,
                opt => opt.MapFrom(src => SerializeReviewDefinition(src.ReviewDefinition)));
        CreateMap<ReviewInstanceInfo, ReviewInstance>();
    }

    /// <summary>
    /// Helper method to serialize the <see cref="ReviewDefinition"/> object to JSON.
    /// </summary>
    /// <param name="reviewDefinition">The review definition to serialize.</param>
    /// <returns>A JSON string representation of the review definition, or null if the review definition is null.</returns>
    private static string? SerializeReviewDefinition(ReviewDefinition? reviewDefinition)
    {
        return reviewDefinition != null ? JsonSerializer.Serialize(reviewDefinition) : null;
    }
}
