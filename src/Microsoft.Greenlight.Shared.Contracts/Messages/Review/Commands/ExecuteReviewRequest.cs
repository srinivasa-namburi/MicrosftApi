using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

/// <summary>
/// This is the first step in the review process. It is used to trigger the review process saga.
/// </summary>
/// <param name="CorrelationId">Review Instance ID</param>
public record ExecuteReviewRequest(Guid CorrelationId) : CorrelatedBy<Guid>
{

}
