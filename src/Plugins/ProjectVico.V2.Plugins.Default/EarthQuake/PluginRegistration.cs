using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Plugins.Default.EarthQuake.Connectors;
using ProjectVico.V2.Plugins.Shared;


namespace ProjectVico.V2.Plugins.Default.EarthQuake;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IEarthquakeConnector, USGSEarthquakeConnector>();

        builder.Services.AddScoped<EarthquakePlugin>();
        return builder;
    }

}

