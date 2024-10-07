using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.DocumentProcess.CustomData.Pipelines;
using Microsoft.Greenlight.DocumentProcess.Shared.Ingestion.Pipelines;
using Microsoft.Greenlight.DocumentProcess.Shared.Ingestion.Classification.Classifiers;
using Microsoft.Greenlight.DocumentProcess.CustomData.Ingestion.Classification.Classifiers;
using Microsoft.Greenlight.Shared.Configuration;

namespace Microsoft.Greenlight.DocumentProcess.CustomData;

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
