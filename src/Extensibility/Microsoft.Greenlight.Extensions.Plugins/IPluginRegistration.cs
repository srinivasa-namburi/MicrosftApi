using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Greenlight.Extensions.Plugins;

public interface IPluginRegistration
{
    void RegisterPlugin(IServiceCollection serviceCollection, IServiceProvider serviceProvider);

}