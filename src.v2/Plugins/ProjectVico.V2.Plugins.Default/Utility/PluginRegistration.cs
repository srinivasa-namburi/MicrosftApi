using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Plugins.Shared;

namespace ProjectVico.V2.Plugins.Default.Utility;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<DatePlugin>();
        builder.Services.AddSingleton<ConversionPlugin>();
        return builder;
    }
}