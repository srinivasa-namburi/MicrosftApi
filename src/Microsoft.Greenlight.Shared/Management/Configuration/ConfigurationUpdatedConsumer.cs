// Microsoft.Greenlight.Shared/Management/Configuration/ConfigurationUpdatedConsumer.cs
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Management.Configuration
{
    /// <summary>
    /// Consumer for configuration update messages.
    /// </summary>
    public class ConfigurationUpdatedConsumer : IConsumer<ConfigurationUpdated>
    {
        private readonly EfCoreConfigurationProvider _configProvider;
        private readonly ILogger<ConfigurationUpdatedConsumer> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationUpdatedConsumer"/> class.
        /// </summary>
        /// <param name="configProvider">The configuration provider.</param>
        /// <param name="logger">The logger.</param>
        public ConfigurationUpdatedConsumer(
            EfCoreConfigurationProvider configProvider,
            ILogger<ConfigurationUpdatedConsumer> logger)
        {
            _configProvider = configProvider;
            _logger = logger;
        }

        /// <summary>
        /// Consumes a configuration update message.
        /// </summary>
        /// <param name="context">The consumer context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task Consume(ConsumeContext<ConfigurationUpdated> context)
        {
            try
            {
                _logger.LogInformation("Received configuration update notification");
                
                _configProvider.Load();

                _logger.LogInformation("Configuration refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing configuration after update notification");
            }
        }
    }
}
