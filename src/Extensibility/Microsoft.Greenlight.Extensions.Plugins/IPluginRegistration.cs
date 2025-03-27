using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Greenlight.Extensions.Plugins;

/// <summary>
/// Interface for registering plugins with the service collection.
/// If you have advanced implementation needs, use the <see cref="IPluginInitializer.InitializeAsync"/> method in addition
/// to this interface.
/// </summary>
public interface IPluginRegistration : IPluginInitializer
{
    /// <summary>
    /// Registers any dependencies (and the plugin itself if neccessary) for the plugin with the service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register the plugin with.</param>

    void RegisterPlugin(IServiceCollection serviceCollection);
}
