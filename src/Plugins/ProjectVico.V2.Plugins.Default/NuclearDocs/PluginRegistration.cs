using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Plugins.Default.NuclearDocs.Services.AiCompletionService;
using ProjectVico.V2.Plugins.Shared;

namespace ProjectVico.V2.Plugins.Default.NuclearDocs;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IAiCompletionService, MultiPassLargeReceiveContextAiCompletionService>();
        builder.Services.AddScoped<NRCDocumentsPlugin>();
        return builder;
    }
}