namespace Microsoft.Greenlight.Worker.DocumentGeneration;

public class DemoWorker : BackgroundService
{
    private readonly ILogger<DemoWorker> _logger;

    public DemoWorker(ILogger<DemoWorker> logger)
    {
        _logger = logger;
    }

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
