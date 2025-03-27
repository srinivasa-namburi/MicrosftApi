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
    private readonly AzureCredentialHelper _credentialHelper;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly string _subscriptionName;
    private readonly string _topicPath;
    private readonly string _pluginTemporaryBasePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShutdownCleanupService"/> class.
    /// This service is responsible for cleaning up resources during application shutdown and is run for every
    /// worker node type except the SetupManager.DB worker type.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="credentialHelper">The Azure credential helper instance.</param>
    public ShutdownCleanupService(IConfiguration configuration, AzureCredentialHelper credentialHelper)
    {
        _credentialHelper = credentialHelper;
        // Get the full name of the current appdomain
        var domainName = AppDomain.CurrentDomain.FriendlyName;

        // Get only the last part from the full domainName
        var domainNameParts = domainName.Split('.');
        var domainNameShort = domainNameParts[^1];

        // Register the restart worker subscription for this node
        _subscriptionName = ServiceBusSubscriptionNameHelper.GetRestartWorkerEndpointName();
        _topicPath = "microsoft.greenlight.shared.contracts.messages/restartworker";

        var serviceBusConnectionString = configuration.GetConnectionString("sbus");
        serviceBusConnectionString = serviceBusConnectionString?.Replace("https://", "sb://").Replace(":443/", "/");

        _adminClient = new ServiceBusAdministrationClient(
            serviceBusConnectionString,
            _credentialHelper.GetAzureCredential());

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
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously: IHostedService requires this method to be async
    public async Task StartAsync(CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously: IHostedService requires this method to be async
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
        await RemoveHostBasedAzureServiceBusEndpoints(cancellationToken);
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

    /// <summary>
    /// Removes Azure Service Bus endpoints based on the host.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task RemoveHostBasedAzureServiceBusEndpoints(CancellationToken cancellationToken)
    {
        if (await _adminClient.SubscriptionExistsAsync(_topicPath, _subscriptionName, cancellationToken))
        {
            await _adminClient.DeleteSubscriptionAsync(_topicPath, _subscriptionName, cancellationToken);
        }
    }
}
