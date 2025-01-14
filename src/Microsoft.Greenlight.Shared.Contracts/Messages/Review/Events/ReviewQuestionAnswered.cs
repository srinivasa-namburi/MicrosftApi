using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

/// <summary>
/// Event raised when a review question is answered (but not yet analyzed for sentiment).
/// </summary>
/// <param name="CorrelationId">The CorrelationId is the ReviewInstanceId</param>
public record ReviewQuestionAnswered(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// The ID of the review question's answer.
    /// </summary>
    public required Guid ReviewQuestionAnswerId { get; init; }
    /// <summary>
    /// The answer to the review question's info.
    /// </summary>
    public required ReviewQuestionAnswerInfo Answer { get; set; }
}
