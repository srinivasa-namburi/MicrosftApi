using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using Aspire.Azure.Messaging.ServiceBus;
using Aspire.Azure.Search.Documents;
using Aspire.Azure.Storage.Blobs;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Exporters;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Plugins;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Embeddings;

namespace Microsoft.Greenlight.Shared.Extensions;
#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001

public static class BuilderExtensions
{

    public static IHostApplicationBuilder AddGreenlightServices(this IHostApplicationBuilder builder, AzureCredentialHelper credentialHelper, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        builder.Services.AddAutoMapper(typeof(DocumentProcessInfoProfile));

        // Common services and dependencies
        builder.AddAzureServiceBusClient("sbus", configureSettings: delegate (AzureMessagingServiceBusSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddAzureSearchClient("aiSearch", configureSettings: delegate (AzureSearchSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddAzureBlobClient("blob-docing", configureSettings: delegate (AzureStorageBlobsSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        // These services use key-based authentication and don't need to use the AzureCredentialHelper.
        // builder.AddKeyedAzureOpenAIClient("openai-planner");

        builder.Services.AddKeyedSingleton<AzureOpenAIClient>("openai-planner", (sp, obj) =>
        {
            // Here's the connection string format. Parse this for endpoint and key
            // Endpoint=https://pvico-oai-swedencentral.openai.azure.com/;Key=18daceb070ee44eea06a277ff0454492

            var connectionString = builder.Configuration.GetConnectionString("openai-planner");
            var endpoint = connectionString!.Split(';')[0].Split('=')[1];
            var key = connectionString!.Split(';')[1].Split('=')[1];

            var openAIClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
            return openAIClient;
        });

        builder.Services.AddScoped<IChatCompletionService>(service =>
            new AzureOpenAIChatCompletionService(serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName,
                service.GetRequiredKeyedService<AzureOpenAIClient>("openai-planner"), "openai-chatcompletion")
        );

        
        builder.Services.AddScoped<ITextEmbeddingGenerationService>(service =>
            new AzureOpenAITextEmbeddingGenerationService(serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName,
                service.GetRequiredKeyedService<AzureOpenAIClient>("openai-planner"), "openai-embeddinggeneration")
        );

        builder.AddRabbitMQClient("rabbitmqdocgen");
        builder.AddRedisClient("redis");

        builder.Services.AddScoped<AzureFileHelper>();
        builder.Services.AddSingleton<SearchClientFactory>();

        builder.Services.AddKeyedTransient<IDocumentExporter, WordDocumentExporter>("IDocumentExporter-Word");

        builder.AddDocGenDbContext(serviceConfigurationOptions);

        builder.Services.AddSingleton<IPluginSourceReferenceCollector, PluginSourceReferenceCollector>();
        builder.Services.AddKeyedScoped<IFunctionInvocationFilter, InputOutputTrackingPluginInvocationFilter>("InputOutputTrackingPluginInvocationFilter");

        return builder;
    }

    public static T? GetServiceForDocumentProcess<T>(this IServiceProvider sp, DocumentProcessInfo documentProcessInfo)
    {
        var service = sp.GetServiceForDocumentProcess<T>(documentProcessInfo.ShortName);
        return service;
    }

    public static T GetRequiredServiceForDocumentProcess<T>(this IServiceProvider sp, DocumentProcessInfo documentProcessInfo)
    {
        var service = sp.GetRequiredServiceForDocumentProcess<T>(documentProcessInfo.ShortName);
        return service;
    }

    public static T GetRequiredServiceForDocumentProcess<T>(this IServiceProvider sp, string documentProcessName)
    {
        var service = sp.GetServiceForDocumentProcess<T>(documentProcessName);
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} not found for document process {documentProcessName}");
        }
        return service;
    }
    public static T? GetServiceForDocumentProcess<T>(this IServiceProvider sp, string documentProcessName)
    {
        T? service = default;

        // Don't use using here as we are retrieving a service to be used outside of the scope of the current request.
        var scope = sp.CreateScope();

        var documentProcessInfoService = scope.ServiceProvider.GetRequiredService<IDocumentProcessInfoService>();
        var documentProcessInfo = documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName).Result;

        if (documentProcessInfo == null && documentProcessName != "Reviews")
        {
            throw new InvalidOperationException($"Document process info not found for {documentProcessName}");
        }

        var dynamicServiceKey = $"Dynamic-{typeof(T).Name}";
        var documentProcessServiceKey = $"{documentProcessName}-{typeof(T).Name}";

        // Try to get a scoped service for the specific document process, then the dynamic service, then the default service, then finally a service with no key.
        // This allows for a service to be registered for a specific document process, or a default service to be registered for all document processes,
        // or a service to be registered with no key for use outside of the document process context.
        service = scope.ServiceProvider.GetKeyedService<T>(documentProcessServiceKey) ??
                  scope.ServiceProvider.GetKeyedService<T>(dynamicServiceKey) ??
                  scope.ServiceProvider.GetKeyedService<T>($"Default-{typeof(T).Name}") ??
                  scope.ServiceProvider.GetService<T>();
        
        // If the service is still null - it may not be scoped but exists as a singleton. Try to get the singleton service.
        if (service == null)
        {
            service = sp.GetKeyedService<T>(documentProcessServiceKey) ??
                      sp.GetKeyedService<T>(dynamicServiceKey) ??
                      sp.GetKeyedService<T>($"Default-{typeof(T).Name}") ??
                      sp.GetService<T>();
        }

        return service;
    }
}
