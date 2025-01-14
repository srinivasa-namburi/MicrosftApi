using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

/// <summary>
/// Event raised when a review question answer is analyzed.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record ReviewQuestionAnswerAnalyzed(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// The ID of the review question's answer
    /// </summary>
    public required Guid ReviewQuestionAnswerId { get; init; }
    /// <summary>
    /// The review question's answer info with sentiment
    /// </summary>
    public required ReviewQuestionAnswerInfo AnswerWithSentiment { get; init; }
}
