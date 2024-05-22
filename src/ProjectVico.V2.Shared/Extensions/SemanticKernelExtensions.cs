using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.TextGeneration;
using ProjectVico.V2.Plugins.Shared;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.Shared.Extensions;
#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001

public static class SemanticKernelExtensions
{
    public static IHostApplicationBuilder AddSemanticKernelServices(this IHostApplicationBuilder builder, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        builder.AddCompletionServices();

        var sp = builder.Services.BuildServiceProvider();

        // UnKeyed Kernel for compatibility. Note - this loads ALL plugins, including document plugins, from all Document Processes.
        builder.Services.AddScoped<Kernel>(serviceProvider =>
        {
            KernelPluginCollection plugins = [];
            plugins.AddRegisteredPluginsToKernelPluginCollection(serviceProvider);
            
            var kernel = new Kernel(serviceProvider, plugins);
            return kernel;
        });

        // Keyed Kernel per Document Process Type
        var documentProcesses = serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses;
        foreach (var documentProcess in documentProcesses)
        {

            builder.Services.AddKeyedScoped<Kernel>(documentProcess.Name+"-Kernel", (provider, o) => 
            {
                KernelPluginCollection plugins = [];
                plugins.AddSharedAndDocumentProcessPluginsToPluginCollection(provider, documentProcess);
                
                var kernel = new Kernel(provider, plugins);
                return kernel;
            });
        }

        return builder;
    }

    private static IHostApplicationBuilder AddCompletionServices(this IHostApplicationBuilder builder)
    {
        var sp = builder.Services.BuildServiceProvider();
        var openAiPlanner = sp.GetKeyedService<OpenAIClient>("openai-planner");
        var serviceConfigurationOptions = builder.Configuration.GetSection("ServiceConfiguration").Get<ServiceConfigurationOptions>()!;

        // Only register the completion services if they don't already exist in the service collection
        if (sp.GetService<IChatCompletionService>() == null)
        {
            builder.Services.AddScoped<IChatCompletionService>(service =>
                new AzureOpenAIChatCompletionService(serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName,
                    openAiPlanner, "openai-chatcompletion")
            );
        }

        if (sp.GetService<ITextEmbeddingGenerationService>() == null)
        {
            builder.Services.AddScoped<ITextEmbeddingGenerationService>(service =>
                new AzureOpenAITextEmbeddingGenerationService(serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName,
                    openAiPlanner, "openai-embeddinggeneration")
            );
        }
        
        if (sp.GetService<ITextGenerationService>() == null)
        {
            builder.Services.AddScoped<ITextGenerationService>(service =>
                new AzureOpenAITextGenerationService(serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName,
                    openAiPlanner, "openai-textgeneration")
            );
        }

        return builder;
    }

}

