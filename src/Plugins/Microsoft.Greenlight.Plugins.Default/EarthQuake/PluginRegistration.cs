using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Plugins.Default.EarthQuake.Connectors;


namespace Microsoft.Greenlight.Plugins.Default.EarthQuake;

public class PluginRegistration : IPluginRegistration
{
    public void RegisterPlugin(IServiceCollection serviceCollection, IServiceProvider serviceProvider)
    {
        serviceCollection.AddScoped<IEarthquakeConnector, USGSEarthquakeConnector>();
        serviceCollection.AddScoped<EarthquakePlugin>();
    }
}

