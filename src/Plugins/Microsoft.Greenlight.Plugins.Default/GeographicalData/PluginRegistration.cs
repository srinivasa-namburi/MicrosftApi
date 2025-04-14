using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Plugins.Default.GeographicalData.Connectors;
using Microsoft.Greenlight.Shared.Plugins;

namespace Microsoft.Greenlight.Plugins.Default.GeographicalData;

public class PluginRegistration : IPluginRegistration
{
    public void RegisterPlugin(IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IMappingConnector, AzureMapsConnector>();
        serviceCollection.AddTransient<FacilitiesPlugin>();
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var registry = serviceProvider.GetRequiredService<IPluginRegistry>();
        registry.AddPlugin("FacilitiesPlugin", serviceProvider.GetRequiredService<FacilitiesPlugin>(), isDynamic: false);
    }
}
