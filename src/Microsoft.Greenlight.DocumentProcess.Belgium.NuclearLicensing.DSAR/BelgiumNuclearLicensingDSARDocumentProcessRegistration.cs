using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Belgium.NuclearLicensing.DSAR.Generation;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Extensions;

namespace Microsoft.Greenlight.DocumentProcess.Belgium.NuclearLicensing.DSAR;

public class BelgiumNuclearLicensingDSARDocumentProcessRegistration : IDocumentProcessRegistration
{
    public string ProcessName => "Belgium.NuclearLicensing.DSAR";
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
                    sp.GetRequiredService<ILogger<KernelMemoryBodyTextGenerator>>(),
                    sp.GetRequiredService<IServiceProvider>()
                ));
        }
        
        builder.Services.AddKeyedScoped<IDocumentOutlineService, BelgiumNuclearLicensingDSARDocumentOutlineService>(ProcessName + "-IDocumentOutlineService");
        // END Generation services
        return builder;
    }
}
