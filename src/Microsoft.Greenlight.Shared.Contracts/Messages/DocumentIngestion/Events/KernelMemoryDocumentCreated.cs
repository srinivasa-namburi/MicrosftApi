using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

/// <summary>
/// Event raised when a document has been created using Kernel Memory.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record KernelMemoryDocumentCreated(Guid CorrelationId) : CorrelatedBy<Guid>;
