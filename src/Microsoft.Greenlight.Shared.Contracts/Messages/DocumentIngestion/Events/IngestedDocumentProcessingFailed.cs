using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

/// <summary>
/// Event raised when processing an ingested document fails.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record IngestedDocumentProcessingFailed(Guid CorrelationId) : CorrelatedBy<Guid>
{

}
