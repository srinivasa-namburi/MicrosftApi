using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Interfaces;

namespace Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing.Plugins.NuclearDocs;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<NRCDocumentsPlugin>();
        return builder;
    }
}
