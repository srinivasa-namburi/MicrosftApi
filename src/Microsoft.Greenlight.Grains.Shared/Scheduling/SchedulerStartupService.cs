using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

public class SchedulerStartupService : IHostedService
{
    private readonly ILogger<SchedulerStartupService> _logger;
    private readonly IGrainFactory _grainFactory;

    public SchedulerStartupService(
        ILogger<SchedulerStartupService> logger,
        IGrainFactory grainFactory)
    {
        _logger = logger;
        _grainFactory = grainFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring scheduler orchestration grain is activated");

        try
        {
            // This just activates the grain, which will auto-start schedulers
            // The grain is a singleton (with fixed key "Scheduler") so only one instance exists across the cluster
            var grain = _grainFactory.GetGrain<ISchedulerOrchestrationGrain>("Scheduler");
            var isActive = await grain.PingAsync();

            // Since there's no GetStateAsync method, we'll let the auto-activation in OnActivateAsync
            // handle starting the schedulers. We can log this intent.
            _logger.LogInformation("Scheduler orchestration grain activated. It will auto-start schedulers if needed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate scheduler orchestration grain");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Stopping scheduler service");

            var grain = _grainFactory.GetGrain<ISchedulerOrchestrationGrain>("Scheduler");
            await grain.StopScheduledJobsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduler service shutdown");
        }
    }
}
