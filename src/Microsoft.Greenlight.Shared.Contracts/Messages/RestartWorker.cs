using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages;

/// <summary>
/// Command to restart a worker.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record RestartWorker(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Restart Web and Frontend nodes. Default is false.
    /// </summary>
    public bool RestartWebNodes { get; set; } = false;

    /// <summary>
    /// Restart API nodes. Default is false.
    /// </summary>
    public bool RestartApiNodes { get; set; } = false;

    /// <summary>
    /// Restart Worker nodes. Default is true.
    /// </summary>
    public bool RestartWorkerNodes { get; set; } = true;

    /// <summary>
    /// Restart System nodes. Default is true.
    /// </summary>
    public bool RestartSystemNodes { get; set; } = true;

}