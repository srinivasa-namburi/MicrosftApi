using Microsoft.Extensions.Hosting;

namespace Microsoft.Greenlight.Extensions.Plugins;

public interface IPluginRegistration
{
    IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder);
}