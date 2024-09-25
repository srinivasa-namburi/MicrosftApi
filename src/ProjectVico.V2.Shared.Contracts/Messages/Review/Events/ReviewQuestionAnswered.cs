using MassTransit;
using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review.Events;

/// <summary>
/// The CorrelationId is the ReviewInstanceId
/// </summary>
/// <param name="CorrelationId"></param>
public record ReviewQuestionAnswered(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required Guid ReviewQuestionAnswerId { get; init; }
    public required ReviewQuestionAnswerInfo Answer { get; set; }
}