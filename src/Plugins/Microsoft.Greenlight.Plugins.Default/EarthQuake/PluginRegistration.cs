using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Plugins.Default.EarthQuake.Connectors;


namespace Microsoft.Greenlight.Plugins.Default.EarthQuake;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IEarthquakeConnector, USGSEarthquakeConnector>();
        builder.Services.AddScoped<EarthquakePlugin>();
        return builder;
    }

}

