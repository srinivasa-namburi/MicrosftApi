using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review.Events;

public record ReviewQuestionAnsweredNotification(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public Guid ReviewQuestionAnswerId { get; set; }
}