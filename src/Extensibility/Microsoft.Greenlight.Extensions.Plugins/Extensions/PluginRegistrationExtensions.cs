using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Greenlight.Extensions.Plugins.Extensions;

public static class PluginRegistrationExtensions
{
    public static IServiceCollection AddScopedKeyedPlugin(this IServiceCollection serviceCollection, Type pluginType)
    {
        var serviceKey = pluginType.GetServiceKeyForPluginType();
        serviceCollection.AddKeyedScoped(pluginType, serviceKey);
        return serviceCollection;
    }

    public static IServiceCollection AddTransientKeyedPlugin(this IServiceCollection serviceCollection, Type pluginType)
    {
        var serviceKey = pluginType.GetServiceKeyForPluginType();
        serviceCollection.AddKeyedTransient(pluginType, serviceKey);
        return serviceCollection;
    }

    public static string GetServiceKeyForPluginType(this Type pluginType)
    {
        var pluginAttribute = pluginType.GetCustomAttribute<GreenlightPluginAttribute>();
        if (pluginAttribute != null && !string.IsNullOrEmpty(pluginAttribute.RegistrationKey))
        {
            return $"DP_{pluginAttribute.RegistrationKey}";
        }
        else
        {
            return $"DP_{pluginType.FullName}";
        }
    }
}