using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Plugins.Default.GeographicalData.Connectors;
using ProjectVico.V2.Shared.Interfaces;

namespace ProjectVico.V2.Plugins.Default.GeographicalData;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IMappingConnector, AzureMapsConnector>();

        builder.Services.AddScoped<FacilitiesPlugin>();
        return builder;
    }
}