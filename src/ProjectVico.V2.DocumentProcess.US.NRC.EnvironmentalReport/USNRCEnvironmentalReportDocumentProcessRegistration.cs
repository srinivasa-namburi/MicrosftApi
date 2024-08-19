using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Prompts;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.DocumentProcess.US.NRC.EnvironmentalReport.Generation;
using ProjectVico.V2.DocumentProcess.US.NRC.EnvironmentalReport.Prompts;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Extensions;

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
        builder.Services.AddKeyedSingleton<IKernelMemoryRepository, KernelMemoryRepository>(ProcessName + "-IKernelMemoryRepository");
        builder.Services.AddKeyedSingleton<IPromptCatalogTypes, USNRCEnvironmentalReportPromptCatalogTypes>(ProcessName+"-IPromptCatalogTypes");
        // END Shared Services

        // Generation services
        if (options.ProjectVicoServices.DocumentGeneration.CreateBodyTextNodes)
        {
            builder.Services.AddKeyedScoped<IBodyTextGenerator, USNRCEnvironmentalReportBodyTextGenerator>(ProcessName + "-IBodyTextGenerator");
        }
        builder.Services.AddKeyedScoped<IAiCompletionService, USNRCEnvironmentalReportAiCompletionService>(ProcessName + "-IAiCompletionService");
        builder.Services.AddKeyedScoped<IDocumentOutlineService, USNRCEnvironmentalReportDocumentOutlineService>(ProcessName + "-IDocumentOutlineService");
        // END Generation services
        return builder;
    }
}