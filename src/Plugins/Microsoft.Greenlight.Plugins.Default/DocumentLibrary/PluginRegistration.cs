using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Plugins;

namespace Microsoft.Greenlight.Plugins.Default.DocumentLibrary;

public class PluginRegistration : IPluginRegistration
{
    public void RegisterPlugin(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<DocumentLibraryPlugin>();
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var registry = serviceProvider.GetRequiredService<IPluginRegistry>();
        registry.AddPlugin("DocumentLibraryPlugin", serviceProvider.GetRequiredService<DocumentLibraryPlugin>(), isDynamic:false);
    }

    
}