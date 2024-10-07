using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

public record DistributeReviewQuestions(Guid CorrelationId) : CorrelatedBy<Guid>
{

}
