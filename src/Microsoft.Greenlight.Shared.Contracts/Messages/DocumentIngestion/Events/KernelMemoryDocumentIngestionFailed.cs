using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record KernelMemoryDocumentIngestionFailed(Guid CorrelationId):CorrelatedBy<Guid>;
