using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.TextGeneration;
using ProjectVico.V2.Plugins.Earthquake.NativePlugins;
using ProjectVico.V2.Plugins.GeographicalData.NativePlugins;
using ProjectVico.V2.Plugins.NuclearDocs.NativePlugins;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Worker.DocumentGeneration.NativePlugins;

namespace ProjectVico.V2.Worker.DocumentGeneration.AI;


public static class SemanticKernelExtensions
{
    private static IHostApplicationBuilder AddSemanticKernelPlugins(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<NuclearDocumentRepositoryPlugin>();
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

        builder.Services.AddScoped<ITextEmbeddingGenerationService>(service => 
            new AzureOpenAITextEmbeddingGenerationService("text-embedding-ada002", 
                openAiPlanner, "openai-embeddinggeneration")
        );

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

            plugins.AddFromObject(sp.GetRequiredService<NuclearDocumentRepositoryPlugin>(), "native_" + nameof(NuclearDocumentRepositoryPlugin));
            plugins.AddFromObject(sp.GetRequiredService<EarthquakePlugin>(), "native_" + nameof(EarthquakePlugin));
            plugins.AddFromObject(sp.GetRequiredService<FacilitiesPlugin>(), "native_" + nameof(FacilitiesPlugin));
            plugins.AddFromObject(sp.GetRequiredService<DatePlugin>(), "native_" + nameof(DatePlugin));
            plugins.AddFromObject(sp.GetRequiredService<ConversionPlugin>(), "native_" + nameof(ConversionPlugin));

            var kernel = new Kernel(serviceProvider, plugins);
            return kernel;
        });

        return builder;
    }
}

