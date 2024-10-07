using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

public record AnalyzeReviewQuestionAnswerSentiment(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required ReviewQuestionAnswerInfo ReviewQuestionAnswer { get; init; }
}
