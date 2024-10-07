using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

public record IngestReviewDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{

}
