using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Classification.Classifiers;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Pipelines;
using ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Generation;
using ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Ingestion.Classification.Classifiers;
using ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Ingestion.Pipelines;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Mappings;

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
            builder.Services.AddScoped<IBodyTextGenerator, USNuclearLicensingSemanticKernelBodyTextGenerator>();
        }
        builder.Services
            .AddKeyedScoped<IDocumentOutlineService, NuclearDocumentOutlineService>(ProcessName + "-IDocumentOutlineService");
        // END Generation services

        return builder;

        
    }
}