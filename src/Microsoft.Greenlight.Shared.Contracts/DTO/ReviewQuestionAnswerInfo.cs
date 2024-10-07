using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public record ReviewQuestionAnswerInfo
{
    public Guid Id { get; set; }
    public Guid ReviewQuestionId { get; set; }
    public Guid ReviewInstanceId { get; set; }
    public string Question { get; set; }
    public string AiAnswer { get; set; }
    public ReviewQuestionAnswerSentiment? AiSentiment { get; set; }
    public string? AiSentimentReasoning { get; set; }
    public ReviewQuestionType QuestionType { get; set; }


}
