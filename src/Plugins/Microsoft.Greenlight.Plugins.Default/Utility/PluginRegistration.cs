using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Interfaces;

namespace Microsoft.Greenlight.Plugins.Default.Utility;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<DatePlugin>();
        builder.Services.AddSingleton<ConversionPlugin>();
        return builder;
    }
}
