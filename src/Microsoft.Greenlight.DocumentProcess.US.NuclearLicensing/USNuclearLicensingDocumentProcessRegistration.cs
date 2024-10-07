using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.DocumentProcess.Shared.Ingestion.Classification.Classifiers;
using Microsoft.Greenlight.DocumentProcess.Shared.Ingestion.Pipelines;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing.Generation;
using Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing.Ingestion.Classification.Classifiers;
using Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing.Ingestion.Pipelines;
using Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing.Search;
using Microsoft.Greenlight.Shared.Configuration;

namespace Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing;

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
        builder.Services
            .AddKeyedScoped<IRagRepository, USNuclearLicensingRagRepository>(ProcessName + "-IRagRepository");
        builder.Services
            .AddScoped<IUSNuclearLicensingRagRepository, USNuclearLicensingRagRepository>();


        // END Shared services

        return builder;
    }
}
