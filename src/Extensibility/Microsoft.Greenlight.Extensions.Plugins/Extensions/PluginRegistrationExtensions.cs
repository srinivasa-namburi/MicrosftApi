using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Greenlight.Extensions.Plugins.Extensions;

/// <summary>
/// Provides extension methods for registering plugins with keyed services.
/// </summary>
public static class PluginRegistrationExtensions
{
    /// <summary>
    /// Adds a scoped keyed plugin to the service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add the plugin to.</param>
    /// <param name="pluginType">The type of the plugin to add.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddScopedKeyedPlugin(this IServiceCollection serviceCollection, Type pluginType)
    {
        var serviceKey = pluginType.GetServiceKeyForPluginType();
        serviceCollection.AddKeyedScoped(pluginType, serviceKey);
        return serviceCollection;
    }

    /// <summary>
    /// Adds a transient keyed plugin to the service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add the plugin to.</param>
    /// <param name="pluginType">The type of the plugin to add.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddTransientKeyedPlugin(this IServiceCollection serviceCollection, Type pluginType)
    {
        var serviceKey = pluginType.GetServiceKeyForPluginType();
        serviceCollection.AddKeyedTransient(pluginType, serviceKey);
        return serviceCollection;
    }

    /// <summary>
    /// Gets the service key for the specified plugin type.
    /// </summary>
    /// <param name="pluginType">The type of the plugin.</param>
    /// <returns>The service key for the plugin type.</returns>
    public static string GetServiceKeyForPluginType(this Type pluginType)
    {
        var pluginAttribute = pluginType.GetCustomAttribute<GreenlightPluginAttribute>();
        if (pluginAttribute != null && !string.IsNullOrEmpty(pluginAttribute.RegistrationKey))
        {
            return $"DP__{pluginAttribute.RegistrationKey}";
        }
        else
        {
            return $"DP__{pluginType.Name?.Replace(".", "_")}";
        }
    }
}
