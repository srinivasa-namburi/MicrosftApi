using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

/// <summary>
/// Command to analyze the sentiment of a review question answer.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record AnalyzeReviewQuestionAnswerSentiment(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// The review question's answer to analyze.
    /// </summary>
    public required ReviewQuestionAnswerInfo ReviewQuestionAnswer { get; init; }
}
