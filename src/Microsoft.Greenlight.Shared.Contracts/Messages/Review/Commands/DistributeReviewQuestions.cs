using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

/// <summary>
/// Command to distribute review questions.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record DistributeReviewQuestions(Guid CorrelationId) : CorrelatedBy<Guid>
{

}
