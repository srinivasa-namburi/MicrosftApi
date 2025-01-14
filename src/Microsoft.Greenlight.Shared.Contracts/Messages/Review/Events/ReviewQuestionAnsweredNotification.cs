using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

/// <summary>
/// Event raised when a review question has fully completed the answering process (answer and sentiment analysis), and
/// ready to notify the UI.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record ReviewQuestionAnsweredNotification(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// The ID of the review question's answer.
    /// </summary>
    public Guid ReviewQuestionAnswerId { get; set; }
}
