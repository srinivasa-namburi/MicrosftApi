using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

/// <summary>
/// Command to ingest a review document.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record IngestReviewDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{

}
