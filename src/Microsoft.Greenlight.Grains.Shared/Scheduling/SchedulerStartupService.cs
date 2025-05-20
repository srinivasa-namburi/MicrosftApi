using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

public class SchedulerStartupService : IHostedService, IDisposable
{
    private readonly ILogger<SchedulerStartupService> _logger;
    private readonly IGrainFactory _grainFactory;
    private readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource _cts = new();
    private Task _keepAliveTask = null!;
    private readonly TimeSpan _pingInterval = TimeSpan.FromSeconds(30);

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
            var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_startupTimeout);
            
            var grain = _grainFactory.GetGrain<ISchedulerOrchestrationGrain>("Scheduler");
            
            // First just activate the grain
            var isActive = await grain.PingAsync()
                .WaitAsync(timeoutCts.Token)
                .ConfigureAwait(false);
            
            _logger.LogInformation("Scheduler orchestration grain activated: {IsActive}", isActive);
            
            // Start the initialization as a grain call - do NOT use Task.Run here
            // Make a direct grain call which will preserve the Orleans threading context
            try
            {
                _logger.LogInformation("Starting scheduler grain initialization");
                var isInitialized = await grain.InitializeAsync()
                    .WaitAsync(timeoutCts.Token)
                    .ConfigureAwait(false);
                _logger.LogInformation("Scheduler grain initialization completed: {IsInitialized}", isInitialized);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Scheduler initialization timed out after {Timeout}. The grain is activated but initialization will continue asynchronously.", _startupTimeout);
            }
            
            // Start the keep-alive task
            _keepAliveTask = KeepAliveLoopAsync(_cts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Scheduler activation was canceled by host shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate scheduler orchestration grain");
            // Don't rethrow - let the application continue running
        }
    }

    private async Task KeepAliveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait before next ping
                await Task.Delay(_pingInterval, cancellationToken);
                
                // Get a fresh reference to the grain each time to avoid using a stale reference
                var grain = _grainFactory.GetGrain<ISchedulerOrchestrationGrain>("Scheduler");
                
                // Send ping with a short timeout
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                
                var isActive = await grain.PingAsync().WaitAsync(linkedCts.Token);
                _logger.LogDebug("Scheduler grain keep-alive ping: {IsActive}", isActive);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal cancellation, exit the loop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduler keep-alive ping. Will retry.");
                
                // Add a small delay before retrying to avoid hammering the system
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Exit if cancelled during the delay
                    break;
                }
            }
        }
        
        _logger.LogInformation("Keep-alive loop ended");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Stopping scheduler service");

            // Cancel the keep-alive task
            _cts.Cancel();
            
            // Wait for the keep-alive task to finish with a timeout
            if (_keepAliveTask != null)
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                var completed = await Task.WhenAny(_keepAliveTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    _logger.LogWarning("Keep-alive task did not complete within timeout during shutdown");
                }
            }

            // Try to gracefully stop the schedulers
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                
                var grain = _grainFactory.GetGrain<ISchedulerOrchestrationGrain>("Scheduler");
                await grain.StopScheduledJobsAsync().WaitAsync(linkedCts.Token);
                _logger.LogInformation("Scheduler service stopped successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Stopping schedulers timed out - continuing shutdown");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduler service shutdown");
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}