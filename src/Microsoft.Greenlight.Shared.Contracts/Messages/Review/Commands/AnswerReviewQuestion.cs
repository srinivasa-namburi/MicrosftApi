using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

/// <summary>
/// Command to answer a review question.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record AnswerReviewQuestion(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Review question information.
    /// </summary>
    public required ReviewQuestionInfo ReviewQuestion { get; init; }
    /// <summary>
    /// The number of the question.
    /// </summary>
    public int? QuestionNumber { get; set; }
    /// <summary>
    /// Number of total questions.
    /// </summary>
    public int? TotalQuestions { get; set; }
}
