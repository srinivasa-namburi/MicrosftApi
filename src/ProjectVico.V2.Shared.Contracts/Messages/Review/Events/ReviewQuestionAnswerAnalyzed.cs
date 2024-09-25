using MassTransit;
using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review.Events;

public record ReviewQuestionAnswerAnalyzed(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required Guid ReviewQuestionAnswerId { get; init; }
    public required ReviewQuestionAnswerInfo AnswerWithSentiment { get; init; }
}