using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.DocumentProcess.US.NRC.EnvironmentalReport.Generation;
using Microsoft.Greenlight.DocumentProcess.US.NRC.EnvironmentalReport.Prompts;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.DocumentProcess.US.NRC.EnvironmentalReport;

public class USNRCEnvironmentalReportDocumentProcessRegistration : IDocumentProcessRegistration
{
    public string ProcessName => "US.NRC.EnvironmentalReport";
    public IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options)
    {
        var process = options.GreenlightServices.DocumentProcesses.SingleOrDefault(x => x?.Name == ProcessName);
        if (process == null)
        {
            throw new InvalidOperationException($"Document process options not found for {ProcessName}");
        }
        
        // Shared Services
        builder.AddKeyedKernelMemoryForDocumentProcess(options, process);
        builder.Services.AddKeyedScoped<IKernelMemoryRepository, KernelMemoryRepository>(ProcessName + "-IKernelMemoryRepository");
        builder.Services.AddKeyedSingleton<IPromptCatalogTypes, USNRCEnvironmentalReportPromptCatalogTypes>(ProcessName+"-IPromptCatalogTypes");
        // END Shared Services

        // Generation services
        
        builder.Services.AddKeyedScoped<IAiCompletionService>(ProcessName + "-IAiCompletionService", (sp, a) =>
            new GenericAiCompletionService(
                sp.GetRequiredService<AiCompletionServiceParameters<GenericAiCompletionService>>(),
                ProcessName
            ));

        if (options.GreenlightServices.DocumentGeneration.CreateBodyTextNodes)
        {
            builder.Services.AddKeyedScoped<IBodyTextGenerator>(ProcessName + "-IBodyTextGenerator", (sp, a) =>
                new KernelMemoryBodyTextGenerator(
                    sp.GetRequiredKeyedService<IAiCompletionService>(ProcessName + "-IAiCompletionService"),
                    sp.GetRequiredService<IConsolidatedSearchOptionsFactory>(),
                    sp.GetRequiredService<ILogger<KernelMemoryBodyTextGenerator>>(),
                    sp.GetRequiredService<IServiceProvider>()
                ));
        }
        
        builder.Services.AddKeyedScoped<IDocumentOutlineService, USNRCEnvironmentalReportDocumentOutlineService>(ProcessName + "-IDocumentOutlineService");
        // END Generation services
        return builder;
    }
}
