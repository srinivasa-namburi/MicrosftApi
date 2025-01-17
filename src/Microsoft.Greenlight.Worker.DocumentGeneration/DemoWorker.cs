namespace Microsoft.Greenlight.Worker.DocumentGeneration;

/// <summary>
/// A simple worker to demonstrate creating a <see cref="BackgroundService"/> in Greenlight."/>
/// </summary>
public class DemoWorker : BackgroundService
{
    private readonly ILogger<DemoWorker> _logger;

    /// <summary>
    /// Instantiates a new instance of <see cref="DemoWorker"/>.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to use for logging.</param>
    public DemoWorker(ILogger<DemoWorker> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(10000, stoppingToken);
        }
    }
}
