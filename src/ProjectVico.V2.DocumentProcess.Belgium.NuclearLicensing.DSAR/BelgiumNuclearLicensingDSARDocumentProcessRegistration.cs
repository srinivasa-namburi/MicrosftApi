using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.DocumentProcess.Belgium.NuclearLicensing.DSAR.Generation;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Extensions;

namespace ProjectVico.V2.DocumentProcess.Belgium.NuclearLicensing.DSAR;

public class BelgiumNuclearLicensingDSARDocumentProcessRegistration : IDocumentProcessRegistration
{
    public string ProcessName => "Belgium.NuclearLicensing.DSAR";
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
        // END Shared Services

        // Ingestion services
        // No Classification service for Belgium.NuclearLicensing.DSAR
        // No Pipeline service for Belgium.NuclearLicensing.DSAR as this is a Kernel Memory only process
        // END Ingestion services

        // Generation services
        if (options.ProjectVicoServices.DocumentGeneration.CreateBodyTextNodes)
        {
            builder.Services.AddKeyedScoped<IBodyTextGenerator, BelgiumNuclearLicensingDSARBodyTextGenerator>(ProcessName + "-IBodyTextGenerator");
        }
        builder.Services.AddKeyedScoped<IAiCompletionService, BelgiumNuclearLicensingDSARAiCompletionService>(ProcessName + "-IAiCompletionService");
        builder.Services.AddKeyedScoped<IDocumentOutlineService, BelgiumNuclearLicensingDSARDocumentOutlineService>(ProcessName + "-IDocumentOutlineService");
        // END Generation services
        return builder;
    }
}