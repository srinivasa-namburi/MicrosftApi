using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Management;

public class ShutdownCleanupService : IHostedService
{
    private readonly AzureCredentialHelper _credentialHelper;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly string _subscriptionName;
    private readonly string _topicPath;
    private readonly string _pluginTemporaryBasePath;

    public ShutdownCleanupService(IConfiguration configuration, AzureCredentialHelper credentialHelper)
    {
        _credentialHelper = credentialHelper;
        // Get the full name of the current appdomain
        var domainName = AppDomain.CurrentDomain.FriendlyName;

        // Get only the last part from the full domainName
        var domainNameParts = domainName.Split('.');
        var domainNameShort = domainNameParts[^1];

        // Register the restart worker subscription for this node
        _subscriptionName = RestartWorkerConsumer.GetRestartWorkerEndpointName();
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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // No action needed on start
        return;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await RemoveHostBasedAzureServiceBusEndpoints(cancellationToken);
        await RemovePluginTemporaryDirectories(cancellationToken);
    }

    private async Task RemovePluginTemporaryDirectories(CancellationToken cancellationToken)
    {
        if (Directory.Exists(_pluginTemporaryBasePath))
        {

            foreach (var directory in Directory.GetDirectories(_pluginTemporaryBasePath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch (Exception ex)
                {
                    continue;
                }
            }
        }
    }

    private async Task RemoveHostBasedAzureServiceBusEndpoints(CancellationToken cancellationToken)
    {
        if (await _adminClient.SubscriptionExistsAsync(_topicPath, _subscriptionName, cancellationToken))
        {
            await _adminClient.DeleteSubscriptionAsync(_topicPath, _subscriptionName, cancellationToken);
        }
    }
}