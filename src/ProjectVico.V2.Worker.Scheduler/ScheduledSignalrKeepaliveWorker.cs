using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages;

namespace ProjectVico.V2.Worker.Scheduler;

public class ScheduledSignalRKeepAliveWorker : BackgroundService
{
    private readonly ILogger<ScheduledSignalRKeepAliveWorker> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IServiceProvider _sp;
    private readonly ServiceConfigurationOptions _options;


    public ScheduledSignalRKeepAliveWorker(
        ILogger<ScheduledSignalRKeepAliveWorker> logger,
        IOptions<ServiceConfigurationOptions> options,
        IServiceProvider sp
       )
    {
        _logger = logger;
        _sp = sp;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = _sp.CreateScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var taskDelayDefaultMilliseconds = Convert.ToInt32(TimeSpan.FromSeconds(20).TotalMilliseconds);
       

        while (!stoppingToken.IsCancellationRequested)
        {
            var taskDelay = taskDelayDefaultMilliseconds;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("ScheduledSignalRKeepAliveWorker ping: {time}", DateTimeOffset.Now);
            }
            
            var correlationId = Guid.NewGuid();
            await publishEndpoint.Publish(new SignalRKeepAlive(correlationId), stoppingToken); 


            await Task.Delay(taskDelay, stoppingToken);
        }
    }

   
}
