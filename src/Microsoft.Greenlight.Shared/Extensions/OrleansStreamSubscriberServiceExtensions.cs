using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Greenlight.Shared.Management;
using Microsoft.Greenlight.Shared.Notifiers;

namespace Microsoft.Greenlight.Shared.Extensions
{
    /// <summary>
    /// Extensions for registering the Orleans stream subscriber service
    /// </summary>
    public static class OrleansStreamSubscriberServiceExtensions
    {
        /// <summary>
        /// Starts the Orleans Stream Subscriber Service as a hosted service.
        /// This should be called after all notifiers have been registered.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection</returns>
        public static IServiceCollection StartOrleansStreamSubscriberService(this IServiceCollection services)
        {
            // Register the service only if not already registered
            services.TryAddSingleton<OrleansStreamSubscriberService>();

            // Register as a hosted service to start it
            services.AddHostedService(provider => provider.GetRequiredService<OrleansStreamSubscriberService>());
            return services;
        }

        /// <summary>
        /// Adds the configuration stream notifier to listen for configuration updates
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection</returns>
        public static IServiceCollection AddConfigurationStreamNotifier(this IServiceCollection services)
        {
            // Register the configuration notifier
            services.TryAddSingleton<ConfigurationWorkStreamNotifier>();

            // Also register as IWorkStreamNotifier so it's discovered by the service
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkStreamNotifier, ConfigurationWorkStreamNotifier>());

            return services;
        }
    }
}
