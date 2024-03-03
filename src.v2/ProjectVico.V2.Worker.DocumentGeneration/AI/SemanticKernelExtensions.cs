using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
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

    public static IHostApplicationBuilder AddSemanticKernelService(this IHostApplicationBuilder builder)
    {
        builder.AddSemanticKernelPlugins();
        var sp = builder.Services.BuildServiceProvider();
        
        var serviceConfigurationOptions = builder.Configuration.GetSection("ServiceConfiguration").Get<ServiceConfigurationOptions>()!;
        var openAiPlanner = sp.GetKeyedService<OpenAIClient>("openai-planner");
        
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

        builder.Services.AddAzureOpenAIChatCompletion(serviceConfigurationOptions.OpenAi.PlannerModelDeploymentName, openAiPlanner, "openai-chatcompletion");
        builder.Services.AddAzureOpenAITextGeneration(serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName, openAiPlanner, "openai-textgeneration");
        builder.Services.AddAzureOpenAITextEmbeddingGeneration("text-embedding-ada002", openAiPlanner, "openai-embeddinggeneration");

        return builder;
    }
}

