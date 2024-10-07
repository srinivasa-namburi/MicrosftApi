using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

public record ReviewQuestionAnsweredNotification(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public Guid ReviewQuestionAnswerId { get; set; }
}
