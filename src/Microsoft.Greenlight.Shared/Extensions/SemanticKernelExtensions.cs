using AutoMapper;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;


namespace Microsoft.Greenlight.Shared.Extensions;
#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001

public static class SemanticKernelExtensions
{
    public static IHostApplicationBuilder AddSemanticKernelServices(this IHostApplicationBuilder builder,
    ServiceConfigurationOptions serviceConfigurationOptions)
    {
        builder.AddCompletionServices();

        // UnKeyed Kernel for compatibility. Note - this loads ALL plugins, including document plugins, from all Document Processes.
        builder.Services.AddScoped<Kernel>(serviceProvider =>
        {
            KernelPluginCollection plugins = [];
            plugins.AddRegisteredPluginsToKernelPluginCollection(serviceProvider);
            var kernel = new Kernel(serviceProvider, plugins);
            return kernel;
        });

        // Get all document processes (both static and dynamic)
        var documentProcesses = GetAllDocumentProcesses(builder.Services, serviceConfigurationOptions);

        foreach (var documentProcess in documentProcesses)
        {
            builder.Services.AddKeyedTransient<Kernel>(documentProcess.ShortName + "-Kernel", (provider, o) =>
            {
                KernelPluginCollection plugins = [];

                // Add plugins from the document process
                plugins.AddSharedAndDocumentProcessPluginsToPluginCollection(provider, documentProcess, null);

                var kernel = new Kernel(provider, plugins);
                return kernel;
            });
        }

        return builder;
    }

    /// <summary>
    /// Removes main repository plugin from plugin collection as it may interfere with generation, where
    /// documents from the main repository are retrieved ahead of execution
    /// </summary>
    /// <param name="kernel">The Semantic Kernel instance (must be instantiated)</param>
    /// <param name="documentProcessName">Document Process to remove KmDocs plugin for. We want to keep other instances</param>
    public static void PrepareSemanticKernelInstanceForGeneration(this Kernel kernel, string documentProcessName)
    {
        var kmDocsPlugins = kernel.Plugins.Where(x => x.Name.Contains("KmDocsPlugin")).ToList();
        
        foreach (var kmDocsPlugin in kmDocsPlugins
                     .Where(kmDocsPlugin => kmDocsPlugin.Name.Contains(documentProcessName) || 
                                            kmDocsPlugin.Name.Contains("native")))
        {
            kernel.Plugins.Remove(kmDocsPlugin);
        }
    }

    private static List<DocumentProcessInfo> GetAllDocumentProcesses(IServiceCollection services, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

        List<DocumentProcessInfo> documentProcesses = [];

        try
        {


            // Get dynamic document processes
            if (dbContext != null && mapper != null)
            {
                var dynamicDocumentProcesses = dbContext.DynamicDocumentProcessDefinitions
                    .Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory)
                    .Include(x => x.DocumentOutline)
                    .ToList();

                foreach (var documentProcess in dynamicDocumentProcesses)
                {
                    var documentProcessInfo = mapper.Map<DocumentProcessInfo>(documentProcess);
                    documentProcesses.Add(documentProcessInfo);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Not yet able to load Dynamic Plugins, possibly due to an in-process upgrade");
            throw;
        }

        // Get static document processes
        var documentProcessOptionsList = serviceConfigurationOptions.GreenlightServices.DocumentProcesses;
        foreach (var documentProcessOptions in documentProcessOptionsList)
        {
            var documentProcess = mapper.Map<DocumentProcessInfo>(documentProcessOptions);
            documentProcesses.Add(documentProcess);
        }

        return documentProcesses;
    }

    private static IHostApplicationBuilder AddCompletionServices(this IHostApplicationBuilder builder)
    {
        var sp = builder.Services.BuildServiceProvider();
        var openAiPlanner = sp.GetKeyedService<AzureOpenAIClient>("openai-planner");
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

        return builder;
    }

}

