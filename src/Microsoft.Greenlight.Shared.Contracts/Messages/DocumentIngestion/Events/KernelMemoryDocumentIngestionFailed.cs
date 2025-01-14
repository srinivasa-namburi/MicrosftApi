using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

/// <summary>
/// Event raised when Kernel Memory fails to ingest a document.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record KernelMemoryDocumentIngestionFailed(Guid CorrelationId):CorrelatedBy<Guid>;
