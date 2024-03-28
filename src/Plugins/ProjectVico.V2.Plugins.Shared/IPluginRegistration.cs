using Microsoft.Extensions.Hosting;

namespace ProjectVico.V2.Plugins.Shared;

public interface IPluginRegistration
{
    IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder);
}