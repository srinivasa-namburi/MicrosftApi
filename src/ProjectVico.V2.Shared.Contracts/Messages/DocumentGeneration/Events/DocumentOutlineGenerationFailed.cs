using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;

public record DocumentOutlineGenerationFailed(Guid CorrelationId) : CorrelatedBy<Guid>;