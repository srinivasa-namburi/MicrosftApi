using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;

public record DocumentOutlineGenerationFailed(Guid CorrelationId) : CorrelatedBy<Guid>;
