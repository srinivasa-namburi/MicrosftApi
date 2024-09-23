using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review.Events;

/// <summary>
/// The CorrelationId is the ReviewInstanceId
/// </summary>
/// <param name="CorrelationId"></param>
public record ReviewQuestionAnswered(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public Guid ReviewQuestionAnswerId { get; set; }
}