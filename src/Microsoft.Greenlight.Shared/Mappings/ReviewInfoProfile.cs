using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping review-related entities to their corresponding DTOs and vice versa.
/// </summary>
public class ReviewInfoProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewInfoProfile"/> class.
    /// Defines the mapping between <see cref="ReviewDefinition"/> and <see cref="ReviewDefinitionInfo"/>,
    /// and between <see cref="ReviewQuestion"/> and <see cref="ReviewQuestionInfo"/>.
    /// </summary>
    public ReviewInfoProfile()
    {
        CreateMap<ReviewDefinition, ReviewDefinitionInfo>();
        CreateMap<ReviewDefinitionInfo, ReviewDefinition>();

        CreateMap<ReviewQuestion, ReviewQuestionInfo>();
        CreateMap<ReviewQuestionInfo, ReviewQuestion>();
    }
}
