using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Plugins.Default.NuclearDocs.Services.AiCompletionService;
using ProjectVico.V2.Plugins.Shared;

namespace ProjectVico.V2.Plugins.Default.NuclearDocs;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        builder.Services.AddKeyedScoped<IAiCompletionService, SinglePassOpenAiCompletionService>("aicompletion-singlepass");
        builder.Services.AddKeyedScoped<IAiCompletionService, MultiPassLargeReceiveContextAiCompletionService>("aicompletion-multipass");

        builder.Services.AddScoped<NuclearDocumentRepositoryPlugin>();
        return builder;
    }
}