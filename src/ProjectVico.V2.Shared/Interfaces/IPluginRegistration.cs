using Microsoft.Extensions.Hosting;

namespace ProjectVico.V2.Shared.Interfaces;

public interface IPluginRegistration
{
    IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder);
}