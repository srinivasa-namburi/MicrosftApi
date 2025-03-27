using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management.Configuration;
using Microsoft.Greenlight.Shared.Management;

namespace Microsoft.Greenlight.Shared.Extensions
{
    /// <summary>
    /// Extension methods for the Mass Transit bus
    /// </summary>
    public static class MassTransitExtensions
    {
        /// <summary>
        /// Add consumers for the worker node that work in a fan out subscription model (messages hit all worker nodes)
        /// </summary>
        /// <param name="configurator"></param>
        public static void AddFanOutConsumersForWorkerNode(this IBusRegistrationConfigurator configurator)
        {
            configurator.AddConsumer<RestartWorkerConsumer>();
            configurator.AddConsumer<ConfigurationUpdatedConsumer>();
        }

        /// <summary>
        /// Add consumers for the non-worker node (API, Web Frontend etc.) that work in a fan out subscription model (messages hit all worker nodes)
        /// </summary>
        /// <param name="configurator"></param>
        public static void AddFanOutConsumersForNonWorkerNode(this IBusRegistrationConfigurator configurator)
        {
            configurator.AddConsumer<RestartWorkerConsumer>();
            configurator.AddConsumer<ConfigurationUpdatedConsumer>();
        }

        /// <summary>
        /// Add fan out subscription endpoints for the worker node
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="context"></param>
        public static void AddFanOutSubscriptionEndpointsForWorkerNode(this IServiceBusBusFactoryConfigurator cfg, IBusRegistrationContext context)
        {
            // Register the restart worker subscription for this node
            var restartWorkerSubscriptionName = ServiceBusSubscriptionNameHelper.GetWorkerEndpointName("rw");
            cfg.SubscriptionEndpoint<RestartWorker>(restartWorkerSubscriptionName, e =>
            {
                e.ConfigureConsumer<RestartWorkerConsumer>(context);
            });

            var configurationUpdatedSubscriptionName = ServiceBusSubscriptionNameHelper.GetWorkerEndpointName("cu");
            cfg.SubscriptionEndpoint<ConfigurationUpdated>(configurationUpdatedSubscriptionName, e =>
            {
                e.ConfigureConsumer<ConfigurationUpdatedConsumer>(context);
            });
        }

        /// <summary>
        /// Add fan out subscription endpoints for the non-worker node (API, Web Frontend etc.)
        /// </summary>
        public static void AddFanOutSubscriptionEndpointsForNonWorkerNode(this IServiceBusBusFactoryConfigurator cfg, IBusRegistrationContext context)
        {
            // Register the restart worker subscription for this node
            var restartWorkerSubscriptionName = ServiceBusSubscriptionNameHelper.GetWorkerEndpointName("rw");
            cfg.SubscriptionEndpoint<RestartWorker>(restartWorkerSubscriptionName, e =>
            {
                e.ConfigureConsumer<RestartWorkerConsumer>(context);
            });

            // Register the configuration updated subscription for this node
            var configurationUpdatedSubscriptionName = ServiceBusSubscriptionNameHelper.GetWorkerEndpointName("cu");
            cfg.SubscriptionEndpoint<ConfigurationUpdated>(configurationUpdatedSubscriptionName, e =>
            {
                e.ConfigureConsumer<ConfigurationUpdatedConsumer>(context);
            });
        }
    }
}
