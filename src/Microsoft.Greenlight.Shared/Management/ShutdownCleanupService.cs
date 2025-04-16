using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Management;

/// <summary>
/// Service to handle cleanup tasks during application shutdown.
/// </summary>
public class ShutdownCleanupService : IHostedService
{
    private readonly string _pluginTemporaryBasePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShutdownCleanupService"/> class.
    /// This service is responsible for cleaning up resources during application shutdown and is run for every
    /// worker node type except the SetupManager.DB worker type.
    /// </summary>
    public ShutdownCleanupService()
    {
       
        // Plugin Cleanup:
        var directoryElements = new List<string>
        {
            "greenlight-plugins",
            Environment.MachineName,
            AppDomain.CurrentDomain.FriendlyName,
            "process-" + Environment.ProcessId.ToString()
        };

        _pluginTemporaryBasePath = Path.Combine(Path.GetTempPath(), Path.Combine(directoryElements.ToArray()));
    }

    /// <summary>
    /// Starts the service.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // No action needed on start
        return;
    }

    /// <summary>
    /// Stops the service.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        RemovePluginTemporaryDirectories(cancellationToken);
    }

    /// <summary>
    /// Removes temporary directories created by plugins.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private void RemovePluginTemporaryDirectories(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_pluginTemporaryBasePath))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(_pluginTemporaryBasePath, "*", SearchOption.AllDirectories))
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch
            {
                continue;
            }
        }
    }
}
