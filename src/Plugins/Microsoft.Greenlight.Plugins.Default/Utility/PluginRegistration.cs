using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Interfaces;

namespace Microsoft.Greenlight.Plugins.Default.Utility;

public class PluginRegistration : IPluginRegistration
{
    public void RegisterPlugin(IServiceCollection serviceCollection, IServiceProvider serviceProvider)
    {
        serviceCollection.AddSingleton<DatePlugin>();
        serviceCollection.AddSingleton<ConversionPlugin>();
    }
}
