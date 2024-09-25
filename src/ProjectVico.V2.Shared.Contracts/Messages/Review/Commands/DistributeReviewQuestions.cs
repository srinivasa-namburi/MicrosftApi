using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review.Commands;

public record DistributeReviewQuestions(Guid CorrelationId) : CorrelatedBy<Guid>
{

}