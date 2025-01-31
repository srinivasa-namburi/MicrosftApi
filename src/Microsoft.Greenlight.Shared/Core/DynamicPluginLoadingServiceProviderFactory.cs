using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Greenlight.Shared.Plugins;

namespace Microsoft.Greenlight.Shared.Core
{
    /// <summary>
    /// A factory for creating service providers that support dynamic plugin loading.
    /// </summary>
    public class DynamicPluginLoadingServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
    {
        private readonly IServiceProviderFactory<IServiceCollection> _innerFactory;
        private IServiceCollection? _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicPluginLoadingServiceProviderFactory"/> class.
        /// </summary>
        /// <param name="innerFactory">The inner factory to delegate to.</param>
        public DynamicPluginLoadingServiceProviderFactory(IServiceProviderFactory<IServiceCollection> innerFactory)
        {
            _innerFactory = innerFactory;
        }

        /// <summary>
        /// Creates a builder for the service collection.
        /// </summary>
        /// <param name="services">The service collection to build.</param>
        /// <returns>The built service collection.</returns>
        public IServiceCollection CreateBuilder(IServiceCollection services)
        {
            _services = services;
            // Add the DynamicPluginContainer and DynamicPluginManager as singletons
            _services.TryAddSingleton<DynamicPluginContainer>();
            _services.TryAddSingleton(sp =>
            {
                var pluginContainer = sp.GetRequiredService<DynamicPluginContainer>();
                var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                return new DynamicPluginManager(serviceScopeFactory, pluginContainer);
            });

            return _innerFactory.CreateBuilder(_services);
        }

        /// <summary>
        /// Creates the service provider from the container builder.
        /// </summary>
        /// <param name="containerBuilder">The container builder to create the service provider from.</param>
        /// <returns>The created service provider.</returns>
        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        {
            // Now that all services are registered, create the final ServiceProvider
            var serviceProvider = _innerFactory.CreateServiceProvider(containerBuilder);

            // Now that the ServiceProvider is built, load plugins
            var pluginManager = serviceProvider.GetRequiredService<DynamicPluginManager>();
            pluginManager.EnsurePluginsLoadedAsync(containerBuilder).GetAwaiter().GetResult();

            // We remove the existing DynamicPluginManager
            containerBuilder.RemoveAll<DynamicPluginManager>();
            containerBuilder.RemoveAll<DynamicPluginContainer>();

            containerBuilder.AddSingleton(pluginManager);
            containerBuilder.AddSingleton(pluginManager.PluginContainer);

            var finalServiceProvider = _innerFactory.CreateServiceProvider(containerBuilder);
            return finalServiceProvider;
        }
    }
}
