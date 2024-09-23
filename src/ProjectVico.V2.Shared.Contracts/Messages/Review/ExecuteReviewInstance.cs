using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review;

public record ExecuteReviewInstance(Guid CorrelationId) : CorrelatedBy<Guid>
{
    
}