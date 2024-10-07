using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record KernelMemoryDocumentIngestionCompleted(Guid CorrelationId):CorrelatedBy<Guid>;
