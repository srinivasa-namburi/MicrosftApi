using System.Security.Cryptography;
using System.Text;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages;

namespace Microsoft.Greenlight.Shared.Management;

/// <summary>
/// Consumer class for handling <see cref="RestartWorker"/> messages.
/// </summary>
public class RestartWorkerConsumer : IConsumer<RestartWorker>
{
    private readonly ILogger<RestartWorkerConsumer> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    /// <summary>
    /// Gets the endpoint name for the <see cref="RestartWorker"/>.
    /// </summary>
    /// <returns>The endpoint name as a string.</returns>
    public static string GetRestartWorkerEndpointName()
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
        _logger.LogWarning("Restart command received. Stopping the worker ...");

        // Signal the application to stop

        _appLifetime.StopApplication();

        // Optionally, wait for any cleanup tasks
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}
