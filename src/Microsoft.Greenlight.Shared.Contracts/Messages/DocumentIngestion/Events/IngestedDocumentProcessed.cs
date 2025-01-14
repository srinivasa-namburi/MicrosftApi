using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

/// <summary>
/// Event raised when an ingested document is processed.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record IngestedDocumentProcessed(Guid CorrelationId) : CorrelatedBy<Guid>
{

};
