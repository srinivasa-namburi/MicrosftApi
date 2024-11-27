using System.Security.Cryptography;
using System.Text;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages;

namespace Microsoft.Greenlight.Shared.Management;

public class RestartWorkerConsumer : IConsumer<RestartWorker>
{
    private readonly ILogger<RestartWorkerConsumer> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public static string GetRestartWorkerEndpointName ()
    {
        var domainName = AppDomain.CurrentDomain.FriendlyName;

        // Get only the last part from the full domainName
        var domainNameParts = domainName.Split('.');
        var domainNameShort = domainNameParts[^1];

        //Compute an MD5 hash based on the machine name
        var machineName = Environment.MachineName;
        var machineNameHashBytes = MD5.HashData(Encoding.UTF8.GetBytes(machineName));
        var machineNameHash = BitConverter.ToString(machineNameHashBytes).Replace("-", "").ToLower();

        machineNameHash = machineNameHash.Substring(0, 14);
        
        // Computed subscription name

        var processId = Environment.ProcessId;
        var subscriptionName = $"rw-{domainNameShort}-{machineNameHash}-{processId}";

        return subscriptionName;
    }
    
    public RestartWorkerConsumer(ILogger<RestartWorkerConsumer> logger, IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _appLifetime = appLifetime;
    }

    public async Task Consume(ConsumeContext<RestartWorker> context)
    {
        _logger.LogWarning("Restart command received. Stopping the worker ...");

        // Signal the application to stop

        _appLifetime.StopApplication();

        // Optionally, wait for any cleanup tasks
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}