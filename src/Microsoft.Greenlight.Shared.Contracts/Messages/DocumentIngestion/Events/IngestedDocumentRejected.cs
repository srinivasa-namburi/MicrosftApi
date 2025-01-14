using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

/// <summary>
/// Event raised when a document is rejected during ingestion.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record IngestedDocumentRejected(Guid CorrelationId) : CorrelatedBy<Guid>;
