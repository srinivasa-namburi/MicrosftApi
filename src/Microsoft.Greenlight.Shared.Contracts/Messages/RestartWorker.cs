using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages;

/// <summary>
/// Command to restart a worker.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record RestartWorker(Guid CorrelationId) : CorrelatedBy<Guid>;