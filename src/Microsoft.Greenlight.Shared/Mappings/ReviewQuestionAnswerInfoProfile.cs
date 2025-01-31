using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping between <see cref="ReviewQuestionAnswer"/> and <see cref="ReviewQuestionAnswerInfo"/>.
/// </summary>
public class ReviewQuestionAnswerInfoProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewQuestionAnswerInfoProfile"/> class.
    /// Defines the mapping between <see cref="ReviewQuestionAnswer"/> and <see cref="ReviewQuestionAnswerInfo"/>.
    /// </summary>
    public ReviewQuestionAnswerInfoProfile()
    {
        CreateMap<ReviewQuestionAnswer, ReviewQuestionAnswerInfo>()
            .ForMember(x => x.Question,
                opt => opt.MapFrom(src => DetermineAvailableQuestionText(src)))
            .ForMember(x => x.ReviewQuestionId,
                opt => opt.MapFrom(src => src.OriginalReviewQuestion != null && src.OriginalReviewQuestion.Id != Guid.Empty ?
                                          src.OriginalReviewQuestion.Id :
                                          src.OriginalReviewQuestionId))
            .ForMember(x => x.AiAnswer, m => m.MapFrom(src => src.FullAiAnswer))
            .ForMember(x => x.QuestionType, m => m.MapFrom(src => src.OriginalReviewQuestionType));

        CreateMap<ReviewQuestionAnswerInfo, ReviewQuestionAnswer>()
            .ForMember(dest => dest.FullAiAnswer, m => m.MapFrom(src => src.AiAnswer))
            .ForMember(dest => dest.OriginalReviewQuestionType, m => m.MapFrom(src => src.QuestionType));
    }

    private static string DetermineAvailableQuestionText(ReviewQuestionAnswer src)
    {
        if (!string.IsNullOrEmpty(src.OriginalReviewQuestionText))
        {
            return src.OriginalReviewQuestionText; // If we have the at creation time question text, use it
        }
        // else use the question text from the original question
        return src.OriginalReviewQuestion != null ? src.OriginalReviewQuestion.Question : "";
    }
}
