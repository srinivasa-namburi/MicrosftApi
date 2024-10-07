using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

/// <summary>
/// The CorrelationId is the ReviewInstanceId
/// </summary>
/// <param name="CorrelationId"></param>
public record ReviewQuestionAnswered(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required Guid ReviewQuestionAnswerId { get; init; }
    public required ReviewQuestionAnswerInfo Answer { get; set; }
}
