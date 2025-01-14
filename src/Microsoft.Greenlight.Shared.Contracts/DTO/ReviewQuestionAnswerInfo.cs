using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents the information related to a review question's answer.
/// </summary>
public record ReviewQuestionAnswerInfo
{
    /// <summary>
    /// Unique identifier of the review question's answer.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier of the review question.
    /// </summary>
    public Guid ReviewQuestionId { get; set; }

    /// <summary>
    /// Unique identifier of the review instance.
    /// </summary>
    public Guid ReviewInstanceId { get; set; }

    /// <summary>
    /// Question text.
    /// </summary>
    public string Question { get; set; }

    /// <summary>
    /// AI-generated answer.
    /// </summary>
    public string AiAnswer { get; set; }

    /// <summary>
    /// Sentiment of the AI-generated answer.
    /// </summary>
    public ReviewQuestionAnswerSentiment? AiSentiment { get; set; }

    /// <summary>
    /// Reasoning behind the AI sentiment.
    /// </summary>
    public string? AiSentimentReasoning { get; set; }

    /// <summary>
    /// Type of the review question.
    /// </summary>
    public ReviewQuestionType QuestionType { get; set; }
}
