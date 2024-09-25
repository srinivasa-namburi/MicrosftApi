using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review.Commands;

public record IngestReviewDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{

}