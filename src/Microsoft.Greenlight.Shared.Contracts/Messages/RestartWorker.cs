using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages;

public record RestartWorker(Guid CorrelationId) : CorrelatedBy<Guid>;