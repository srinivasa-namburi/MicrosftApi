using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

/// <summary>
/// Event raised when processing of an ingested document is stopped due to an unsupported classification.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record IngestedDocumentProcessingStoppedByUnsupportedClassification(Guid CorrelationId) : CorrelatedBy<Guid>;
