using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Streams;
using Microsoft.Greenlight.Shared.Management.Configuration;
using Orleans.Streams;

namespace Microsoft.Greenlight.Shared.Notifiers
{
    /// <summary>
    /// Configuration stream notifier for handling configuration update events
    /// </summary>
    public class ConfigurationWorkStreamNotifier : IWorkStreamNotifier
    {
        private readonly ILogger<ConfigurationWorkStreamNotifier> _logger;
        private readonly IServiceProvider _sp;
        private readonly IEnumerable<IConfigurationProvider> _configurationProviders;

        public string Name => "Configuration";

        public ConfigurationWorkStreamNotifier(
            ILogger<ConfigurationWorkStreamNotifier> logger,
            IEnumerable<IConfigurationRoot> configurationSources,
            IServiceProvider sp)
        {
            _logger = logger;
            _sp = sp;
            _configurationProviders = configurationSources
                .SelectMany(config => config.Providers)
                .ToList();
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

                var efCoreProvider = scope.ServiceProvider.GetRequiredService<EfCoreConfigurationProvider>();
                // Register a subscription for configuration updates
                var configSubscription = await streamProvider
                    .GetStream<ConfigurationUpdated>(SystemStreamNameSpaces.ConfigurationUpdatedNamespace, Guid.Empty)
                    .SubscribeAsync(new ConfigurationUpdateObserver(efCoreProvider, _logger));

                subscriptionHandles.Add(configSubscription);
                _logger.LogInformation("Subscribed to configuration update notifications");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up configuration stream subscription");
                throw;
            }

            return subscriptionHandles;
        }

        /// <inheritdoc />
        public async Task UnsubscribeFromStreamsAsync(IEnumerable<object> subscriptionHandles)
        {
            foreach (var handle in subscriptionHandles)
            {
                if (handle is StreamSubscriptionHandle<ConfigurationUpdated> configHandle)
                {
                    await configHandle.UnsubscribeAsync();
                }
            }
        }
    }

    /// <summary>
    /// Observer for configuration update notifications
    /// </summary>
    public class ConfigurationUpdateObserver : IAsyncObserver<ConfigurationUpdated>
    {
        private readonly EfCoreConfigurationProvider _configProvider;
        private readonly ILogger _logger;

        public ConfigurationUpdateObserver(
            EfCoreConfigurationProvider configProvider,
            ILogger logger)
        {
            _configProvider = configProvider;
            _logger = logger;
        }

        public Task OnCompletedAsync() => Task.CompletedTask;

        public Task OnErrorAsync(Exception ex)
        {
            _logger.LogError(ex, "Error in ConfigurationUpdateObserver");
            return Task.CompletedTask;
        }

        public Task OnNextAsync(ConfigurationUpdated notification, StreamSequenceToken? token = null)
        {
            try
            {
                _logger.LogInformation("Received configuration update notification with correlation ID: {CorrelationId}",
                    notification.CorrelationId);

                // Trigger reload
                _configProvider.Load();

                _logger.LogInformation("Configuration reload completed for correlation ID: {CorrelationId}",
                    notification.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling configuration update for correlation ID: {CorrelationId}",
                    notification.CorrelationId);
            }

            return Task.CompletedTask;
        }
    }
}