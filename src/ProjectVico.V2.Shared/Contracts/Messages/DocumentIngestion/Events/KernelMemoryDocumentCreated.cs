using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record KernelMemoryDocumentCreated(Guid CorrelationId) : CorrelatedBy<Guid>;