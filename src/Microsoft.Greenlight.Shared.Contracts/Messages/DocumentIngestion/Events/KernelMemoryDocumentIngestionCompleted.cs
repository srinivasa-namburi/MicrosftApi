using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

/// <summary>
/// Event raised when Kernel Memory completes ingesting a document.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record KernelMemoryDocumentIngestionCompleted(Guid CorrelationId):CorrelatedBy<Guid>;
