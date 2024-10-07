using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

public record ReviewQuestionAnswerAnalyzed(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required Guid ReviewQuestionAnswerId { get; init; }
    public required ReviewQuestionAnswerInfo AnswerWithSentiment { get; init; }
}
