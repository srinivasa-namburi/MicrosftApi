using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.CustomData.Pipelines;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Pipelines;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Classification.Classifiers;
using ProjectVico.V2.DocumentProcess.CustomData.Ingestion.Classification.Classifiers;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.DocumentProcess.CustomData;

public class CustomDataDocumentProcessRegistration : IDocumentProcessRegistration
{
    public string ProcessName => "CustomData";
    public IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options)
    {
        builder.Services.AddKeyedScoped<IDocumentClassifier, CustomDataDocumentClassifier>(ProcessName + "-IDocumentClassifier");
        builder.Services.AddKeyedScoped<IPdfPipeline, BaselinePipeline>(ProcessName + "-IPdfPipeline");
        return builder;
    }
}