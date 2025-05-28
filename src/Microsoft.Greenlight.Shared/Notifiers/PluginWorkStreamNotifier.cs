using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Streams;
using Microsoft.Greenlight.Shared.Plugins;
using Orleans.Streams;

namespace Microsoft.Greenlight.Shared.Notifiers
{
    /// <summary>
    /// Plugin stream notifier for handling plugin update events
    /// </summary>
    public class PluginWorkStreamNotifier : IWorkStreamNotifier
    {
        private readonly ILogger<PluginWorkStreamNotifier> _logger;
        private readonly IServiceProvider _sp;

        public string Name => "Plugin";

        public PluginWorkStreamNotifier(
            ILogger<PluginWorkStreamNotifier> logger,
            IServiceProvider sp)
        {
            _logger = logger;
            _sp = sp;
        }

        /// <inheritdoc />
        public async Task<List<object>> SubscribeToStreamsAsync(
            IClusterClient clusterClient,
            IStreamProvider streamProvider)
        {
            var subscriptionHandles = new List<object>();

            try
            {
                using var scope = _sp.CreateScope();
                var pluginManager = scope.ServiceProvider.GetRequiredService<McpPluginManager>();
                var pluginSubscription = await streamProvider
                    .GetStream<PluginUpdate>(SystemStreamNameSpaces.PluginUpdateNamespace, Guid.Empty)
                    .SubscribeAsync(new PluginUpdateObserver(pluginManager, _logger));
                subscriptionHandles.Add(pluginSubscription);
                _logger.LogInformation("Subscribed to plugin update notifications");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up plugin stream subscription");
                throw;
            }

            return subscriptionHandles;
        }

        /// <inheritdoc />
        public async Task UnsubscribeFromStreamsAsync(IEnumerable<object> subscriptionHandles)
        {
            foreach (var handle in subscriptionHandles)
            {
                if (handle is StreamSubscriptionHandle<PluginUpdate> pluginHandle)
                {
                    await pluginHandle.UnsubscribeAsync();
                }
            }
        }
    }

    /// <summary>
    /// Observer for plugin update notifications
    /// </summary>
    public class PluginUpdateObserver : IAsyncObserver<PluginUpdate>
    {
        private readonly McpPluginManager _pluginManager;
        private readonly ILogger _logger;

        public PluginUpdateObserver(
            McpPluginManager pluginManager,
            ILogger logger)
        {
            _pluginManager = pluginManager;
            _logger = logger;
        }

        public Task OnCompletedAsync() => Task.CompletedTask;

        public Task OnErrorAsync(Exception ex)
        {
            _logger.LogError(ex, "Error in PluginUpdateObserver");
            return Task.CompletedTask;
        }

        public async Task OnNextAsync(PluginUpdate notification, StreamSequenceToken? token = null)
        {
            try
            {
                _logger.LogInformation("Received plugin update notification for {PluginName} version {Version} (CorrelationId: {CorrelationId})", notification.PluginName, notification.VersionString, notification.CorrelationId);
                await _pluginManager.StopAndRemovePluginVersionAsync(notification.PluginName, notification.VersionString);
                _logger.LogInformation("Plugin stop/remove completed for {PluginName} version {Version}", notification.PluginName, notification.VersionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling plugin update for {PluginName} version {Version} (CorrelationId: {CorrelationId})", notification.PluginName, notification.VersionString, notification.CorrelationId);
            }
        }
    }
}
