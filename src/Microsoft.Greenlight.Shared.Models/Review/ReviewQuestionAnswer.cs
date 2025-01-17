using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.Review;

/// <summary>
/// Represents an answer to a review question, including AI-generated sentiment and reasoning.
/// </summary>
public class ReviewQuestionAnswer : EntityBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewQuestionAnswer"/> class.
    /// </summary>
    public ReviewQuestionAnswer()
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewQuestionAnswer"/> class with the specified review question.
    /// </summary>
    /// <param name="question">The review question to create the answer from.</param>
    public ReviewQuestionAnswer(ReviewQuestion question)
    {
        CreateFromReviewQuestion(question);
    }

    /// <summary>
    /// Unique identifier of the review instance.
    /// </summary>
    public Guid ReviewInstanceId { get; set; }

    /// <summary>
    /// Review instance associated with this answer.
    /// </summary>
    [JsonIgnore]
    public ReviewInstance? ReviewInstance { get; set; }

    /// <summary>
    /// Unique identifier of the original review question.
    /// </summary>
    public Guid? OriginalReviewQuestionId { get; set; }

    /// <summary>
    /// Original review question associated with this answer.
    /// </summary>
    [JsonIgnore]
    public ReviewQuestion? OriginalReviewQuestion { get; set; }

    /// <summary>
    /// Text of the original review question.
    /// </summary>
    public required string OriginalReviewQuestionText { get; set; }

    /// <summary>
    /// Type of the original review question.
    /// </summary>
    public ReviewQuestionType OriginalReviewQuestionType { get; set; }

    /// <summary>
    /// Full AI-generated answer.
    /// </summary>
    public string? FullAiAnswer { get; set; }

    /// <summary>
    /// AI-generated sentiment of the answer.
    /// </summary>
    public ReviewQuestionAnswerSentiment? AiSentiment { get; set; }

    /// <summary>
    /// Reasoning behind the AI-generated sentiment.
    /// </summary>
    public string? AiSentimentReasoning { get; set; }

    /// <summary>
    /// Creates an answer from the specified review question.
    /// </summary>
    /// <param name="question">The review question to create the answer from.</param>
    public void CreateFromReviewQuestion(ReviewQuestion question)
    {
        OriginalReviewQuestionId = question.Id;
        OriginalReviewQuestionText = question.Question;
        OriginalReviewQuestionType = question.QuestionType;
    }
}
