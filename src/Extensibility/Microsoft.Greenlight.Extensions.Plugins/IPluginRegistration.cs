using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Greenlight.Extensions.Plugins;

/// <summary>
/// Interface for registering plugins with the service collection.
/// </summary>
public interface IPluginRegistration
{
    /// <summary>
    /// Registers a plugin with the specified service collection and service provider.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register the plugin with.</param>
    /// <param name="serviceProvider">The service provider to use for plugin registration.</param>
    void RegisterPlugin(IServiceCollection serviceCollection, IServiceProvider serviceProvider);
}
