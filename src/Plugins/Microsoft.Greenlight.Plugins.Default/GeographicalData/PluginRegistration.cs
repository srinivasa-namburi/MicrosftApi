using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Plugins.Default.GeographicalData.Connectors;

namespace Microsoft.Greenlight.Plugins.Default.GeographicalData;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IMappingConnector, AzureMapsConnector>();
        builder.Services.AddScoped<FacilitiesPlugin>();
        return builder;
    }
}
