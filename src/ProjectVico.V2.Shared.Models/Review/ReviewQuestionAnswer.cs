using System.Text.Json.Serialization;
using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Shared.Models.Review;

public class ReviewQuestionAnswer : EntityBase
{
    public ReviewQuestionAnswer()
    {
        
    }

    public ReviewQuestionAnswer(ReviewQuestion question)
    {
        CreateFromReviewQuestion(question);
    }

    public Guid ReviewInstanceId { get; set; }
    [JsonIgnore]
    public ReviewInstance? ReviewInstance { get; set; }

    public Guid? OriginalReviewQuestionId { get; set; }
    [JsonIgnore]
    public ReviewQuestion? OriginalReviewQuestion { get; set; }

    public string OriginalReviewQuestionText { get; set; }
    public ReviewQuestionType OriginalReviewQuestionType { get; set; }

    public string? FullAiAnswer { get; set; }
    public ReviewQuestionAnswerSentiment? AiSentiment { get; set; }
    public string? AiSentimentReasoning { get; set; }

    public void CreateFromReviewQuestion(ReviewQuestion question)
    {
        OriginalReviewQuestionId = OriginalReviewQuestionId;
        OriginalReviewQuestionText = question.Question;
        OriginalReviewQuestionType = question.QuestionType;
    }

}