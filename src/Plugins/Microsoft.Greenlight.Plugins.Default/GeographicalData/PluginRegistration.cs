using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Plugins.Default.GeographicalData.Connectors;

namespace Microsoft.Greenlight.Plugins.Default.GeographicalData;

public class PluginRegistration : IPluginRegistration
{
    public void RegisterPlugin(IServiceCollection serviceCollection, IServiceProvider serviceProvider)
    {
        serviceCollection.AddScoped<IMappingConnector, AzureMapsConnector>();
        serviceCollection.AddScoped<FacilitiesPlugin>();
    }
}
