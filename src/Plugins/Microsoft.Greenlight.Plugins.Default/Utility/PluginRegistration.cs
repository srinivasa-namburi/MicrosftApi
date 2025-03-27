using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Plugins;


namespace Microsoft.Greenlight.Plugins.Default.Utility;

public class PluginRegistration : IPluginRegistration
{
    public void RegisterPlugin(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<DatePlugin>();
        serviceCollection.AddSingleton<ConversionPlugin>();
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var registry = serviceProvider.GetRequiredService<IPluginRegistry>();
        registry.AddPlugin("DatePlugin", serviceProvider.GetRequiredService<DatePlugin>(), isDynamic: false);
        registry.AddPlugin("ConversionPlugin", serviceProvider.GetRequiredService<ConversionPlugin>(), isDynamic: false);

    }

}
