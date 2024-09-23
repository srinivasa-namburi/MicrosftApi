using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.DocumentProcess.US.NRC.EnvironmentalReport.Generation;
using ProjectVico.V2.DocumentProcess.US.NRC.EnvironmentalReport.Prompts;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Prompts;

namespace ProjectVico.V2.DocumentProcess.US.NRC.EnvironmentalReport;

public class USNRCEnvironmentalReportDocumentProcessRegistration : IDocumentProcessRegistration
{
    public string ProcessName => "US.NRC.EnvironmentalReport";
    public IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options)
    {
        var process = options.ProjectVicoServices.DocumentProcesses.SingleOrDefault(x => x?.Name == ProcessName);
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

        if (options.ProjectVicoServices.DocumentGeneration.CreateBodyTextNodes)
        {
            builder.Services.AddKeyedScoped<IBodyTextGenerator>(ProcessName + "-IBodyTextGenerator", (sp, a) =>
                new KernelMemoryBodyTextGenerator(
                    sp.GetRequiredKeyedService<IAiCompletionService>(ProcessName + "-IAiCompletionService"),
                    sp.GetRequiredService<ILogger<KernelMemoryBodyTextGenerator>>(),
                    sp.GetRequiredService<IServiceProvider>()
                ));
        }
        
        builder.Services.AddKeyedScoped<IDocumentOutlineService, USNRCEnvironmentalReportDocumentOutlineService>(ProcessName + "-IDocumentOutlineService");
        // END Generation services
        return builder;
    }
}