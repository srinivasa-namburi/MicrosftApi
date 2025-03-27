using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Management;

/// <summary>
/// Consumer class for handling <see cref="RestartWorker"/> messages.
/// </summary>
public class RestartWorkerConsumer : IConsumer<RestartWorker>
{
    private readonly ILogger<RestartWorkerConsumer> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="RestartWorkerConsumer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="appLifetime">The application lifetime instance.</param>
    public RestartWorkerConsumer(ILogger<RestartWorkerConsumer> logger, IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _appLifetime = appLifetime;
    }

    /// <summary>
    /// Consumes the <see cref="RestartWorker"/> message and stops the application.
    /// </summary>
    /// <param name="context">The consume context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<RestartWorker> context)
    {
        _logger.LogInformation("Restart command received. Making restart determination based on node type.");

        if (DetermineNodeShouldBeRestarted(context.Message))
        {
            _logger.LogInformation("Restarting worker node.");
            // Signal the application to stop
            _appLifetime.StopApplication();

            // Optionally, wait for any cleanup tasks
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        else
        {
            _logger.LogInformation("Determined this node should not be restarted due to RestartWorker configuration");
        }
        
    }

    private bool DetermineNodeShouldBeRestarted(RestartWorker contextMessage)
    {
        var workerNodeType = AdminHelper.DetermineCurrentlyRunningWorkerNodeType();
        _logger.LogInformation($"Restart determination - Worker node type: {workerNodeType}");

        // Determine if the worker node should be restarted based on the boolean is set to true
        // for the type that is running

        switch (workerNodeType)
        {
            case WorkerNodeType.Web:
                return contextMessage.RestartWebNodes;
            case WorkerNodeType.Api:
                return contextMessage.RestartApiNodes;
            case WorkerNodeType.Worker:
                return contextMessage.RestartWorkerNodes;
            case WorkerNodeType.System:
                return contextMessage.RestartSystemNodes;
            default:
                return false;
        }
    }
}
