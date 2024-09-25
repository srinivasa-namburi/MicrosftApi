using MassTransit;
using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review.Commands;

public record AnalyzeReviewQuestionAnswerSentiment(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required ReviewQuestionAnswerInfo ReviewQuestionAnswer { get; init; }
}