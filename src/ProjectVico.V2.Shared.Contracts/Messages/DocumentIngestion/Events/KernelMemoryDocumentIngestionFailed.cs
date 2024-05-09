using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record KernelMemoryDocumentIngestionFailed(Guid CorrelationId):CorrelatedBy<Guid>;