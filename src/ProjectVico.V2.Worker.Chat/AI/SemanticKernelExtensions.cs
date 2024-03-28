using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.TextGeneration;
using ProjectVico.V2.Plugins.Default.EarthQuake;
using ProjectVico.V2.Plugins.Default.GeographicalData;
using ProjectVico.V2.Plugins.Default.NuclearDocs;
using ProjectVico.V2.Plugins.Default.Utility;
using ProjectVico.V2.Plugins.Shared;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.Worker.Chat.AI;


public static class SemanticKernelExtensions
{
    private static IHostApplicationBuilder AddSemanticKernelPlugins(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<NRCDocumentsPlugin>();
        builder.Services.AddScoped<EarthquakePlugin>();
        builder.Services.AddScoped<FacilitiesPlugin>();
        builder.Services.AddSingleton<DatePlugin>();
        builder.Services.AddSingleton<ConversionPlugin>();

        return builder;
    }

    private static IHostApplicationBuilder AddCompletionServices(this IHostApplicationBuilder builder)
    {
        var sp = builder.Services.BuildServiceProvider();
        var openAiPlanner = sp.GetKeyedService<OpenAIClient>("openai-planner");
        var serviceConfigurationOptions = builder.Configuration.GetSection("ServiceConfiguration").Get<ServiceConfigurationOptions>()!;

        builder.Services.AddScoped<IChatCompletionService>(service =>
            new AzureOpenAIChatCompletionService(serviceConfigurationOptions.OpenAi.PlannerModelDeploymentName,
                openAiPlanner, "openai-chatcompletion")
        );

#pragma warning disable SKEXP0011;SKEXP0001
        builder.Services.AddScoped<ITextEmbeddingGenerationService>(service =>

            new AzureOpenAITextEmbeddingGenerationService("text-embedding-ada002",
                openAiPlanner, "openai-embeddinggeneration")
        );
#pragma warning restore SKEXP0011;SKEXP0001

        builder.Services.AddScoped<ITextGenerationService>(service =>
            new AzureOpenAITextGenerationService(serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
                openAiPlanner, "openai-textgeneration")
        );
        return builder;
    }

    public static IHostApplicationBuilder AddSemanticKernelService(this IHostApplicationBuilder builder)
    {
        builder.AddCompletionServices();
        builder.AddSemanticKernelPlugins();

        var sp = builder.Services.BuildServiceProvider();

        builder.Services.AddScoped<Kernel>(serviceProvider =>
        {
            KernelPluginCollection plugins = new KernelPluginCollection();

            plugins.AddRegisteredPluginsToKernelPluginCollection(serviceProvider);

            var kernel = new Kernel(serviceProvider, plugins);
            return kernel;
        });

        return builder;
    }
}

