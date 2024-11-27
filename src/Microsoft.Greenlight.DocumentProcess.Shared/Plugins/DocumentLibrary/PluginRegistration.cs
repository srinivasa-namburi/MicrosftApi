using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Extensions.Plugins;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Plugins.DocumentLibrary;

public class PluginRegistration : IPluginRegistration
{
    public void RegisterPlugin(IServiceCollection serviceCollection, IServiceProvider serviceProvider)
    {
        serviceCollection.AddSingleton<DocumentLibraryPlugin>();
    }
}