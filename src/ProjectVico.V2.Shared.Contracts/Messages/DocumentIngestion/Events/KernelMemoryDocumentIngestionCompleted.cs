using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record KernelMemoryDocumentIngestionCompleted(Guid CorrelationId):CorrelatedBy<Guid>;