using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Plugins.Shared;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Plugins.NuclearDocs;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<NRCDocumentsPlugin>();
        return builder;
    }
}