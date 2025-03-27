using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Plugins.Default.EarthQuake.Connectors;
using Microsoft.Greenlight.Shared.Plugins;


namespace Microsoft.Greenlight.Plugins.Default.EarthQuake;

public class PluginRegistration : IPluginRegistration
{
    public void RegisterPlugin(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IEarthquakeConnector, USGSEarthquakeConnector>();
        serviceCollection.AddScoped<EarthquakePlugin>();
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var earthQuakeConnector = serviceProvider.GetRequiredService<IEarthquakeConnector>();
        var registry = serviceProvider.GetRequiredService<IPluginRegistry>();

        registry.AddPlugin("EarthQuakePlugin", serviceProvider.GetRequiredService<EarthquakePlugin>(), isDynamic:false);
    }
}

