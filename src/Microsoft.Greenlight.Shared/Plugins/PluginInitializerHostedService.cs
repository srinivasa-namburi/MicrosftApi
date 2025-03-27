using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Hosted service that initializes all plugins, both dynamic and static.
    /// </summary>
    public class PluginInitializerHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        /// <summary>
        /// The PluginInitializerHostedService constructor - takes in the service provider.
        /// </summary>
        /// <param name="serviceProvider"></param>
        public PluginInitializerHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var initializers = _serviceProvider.GetServices<IPluginInitializer>();
            foreach (var initializer in initializers)
            {
                await initializer.InitializeAsync(_serviceProvider);
            }
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}