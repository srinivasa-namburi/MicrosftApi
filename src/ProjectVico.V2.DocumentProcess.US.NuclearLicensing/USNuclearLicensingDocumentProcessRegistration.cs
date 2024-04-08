using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Classification.Classifiers;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Pipelines;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Generation;
using ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Ingestion.Classification.Classifiers;
using ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Ingestion.Pipelines;
using ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Mapping;
using ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Search;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing;

public class USNuclearLicensingDocumentProcessRegistration : IDocumentProcessRegistration
{
    public string ProcessName => "US.NuclearLicensing";

    public IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options)
    {
        var process = options.ProjectVicoServices.DocumentProcesses.SingleOrDefault(x => x?.Name == ProcessName);
        if (process == null)
        {
            throw new InvalidOperationException($"Document process options not found for {ProcessName}");
        }
        
        // Ingestion services
        builder.Services
            .AddKeyedScoped<IDocumentClassifier, NrcAdamsDocumentClassifier>(ProcessName + "-IDocumentClassifier");
        builder.Services
            .AddKeyedScoped<IPdfPipeline, NuclearEnvironmentalReportPdfPipeline>(ProcessName + "-IPdfPipeline");
        // END Ingestion services
        
        // Generation services
        if (options.ProjectVicoServices.DocumentGeneration.CreateBodyTextNodes)
        {
            builder.Services.AddKeyedScoped<IBodyTextGenerator, USNuclearLicensingBodyTextGenerator>(ProcessName + "-IBodyTextGenerator");
        }
        builder.Services.AddKeyedScoped<IAiCompletionService, MultiPassLargeReceiveContextAiCompletionService>(ProcessName + "-IAiCompletionService");
        builder.Services.AddKeyedScoped<IDocumentOutlineService, NuclearDocumentOutlineService>(ProcessName + "-IDocumentOutlineService");
        // END Generation services

        // Shared services
        builder.Services.AddAutoMapper(typeof(USNuclearLicensingMetadataProfile));
        builder.Services
            .AddKeyedScoped<IRagRepository, USNuclearLicensingRagRepository>(ProcessName + "-IRagRepository");
        builder.Services
            .AddScoped<IUSNuclearLicensingRagRepository, USNuclearLicensingRagRepository>();
        // END Shared services

        return builder;
    }
}