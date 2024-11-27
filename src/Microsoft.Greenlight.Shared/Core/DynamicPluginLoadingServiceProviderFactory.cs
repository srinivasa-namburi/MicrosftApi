using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Greenlight.Shared.Plugins;

namespace Microsoft.Greenlight.Shared.Core
{
    public class DynamicPluginLoadingServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
    {
        private readonly IServiceProviderFactory<IServiceCollection> _innerFactory;
        private IServiceCollection? _services;

        public DynamicPluginLoadingServiceProviderFactory(IServiceProviderFactory<IServiceCollection> innerFactory)
        {
            _innerFactory = innerFactory;
        }

        public IServiceCollection CreateBuilder(IServiceCollection services)
        {
            _services = services;
            // Add the DynamicPluginContainer and DynamicPluginManager as singletons
            _services.TryAddSingleton<DynamicPluginContainer>();
            _services.TryAddSingleton<DynamicPluginManager>(sp =>
            {
                var pluginContainer = sp.GetRequiredService<DynamicPluginContainer>();
                var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                return new DynamicPluginManager(_services, serviceScopeFactory, pluginContainer);
            });

            return _innerFactory.CreateBuilder(_services);
        }


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
