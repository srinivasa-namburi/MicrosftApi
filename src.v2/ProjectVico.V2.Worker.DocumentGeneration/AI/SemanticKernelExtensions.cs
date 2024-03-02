using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using ProjectVico.V2.Plugins.Earthquake.Connectors;
using ProjectVico.V2.Plugins.Earthquake.NativePlugins;
using ProjectVico.V2.Plugins.GeographicalData.Connectors;
using ProjectVico.V2.Plugins.GeographicalData.NativePlugins;
using ProjectVico.V2.Plugins.NuclearDocs.NativePlugins;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Search;
using ProjectVico.V2.Worker.DocumentGeneration.NativePlugins;

namespace ProjectVico.V2.Worker.DocumentGeneration.AI;


public static class SemanticKernelExtensions
{
    public static IHostApplicationBuilder AddSemanticKernelService(this IHostApplicationBuilder builder)
    {

        var sp = builder.Services.BuildServiceProvider();

        var openAiPlanner = sp.GetKeyedService<OpenAIClient>("openai-planner");
       
        var openAiDocGen1 = sp.GetKeyedService<OpenAIClient>("openai-docgen1");

        var configuration = builder.Configuration;

        var openApiPluginsFromConfiguration = configuration
            .AsEnumerable()
            .Where(kvp =>
                kvp.Key.StartsWith("Services:plugin-", StringComparison.OrdinalIgnoreCase) &&
                kvp.Key.EndsWith(":0", StringComparison.OrdinalIgnoreCase) &&
                kvp.Value != null &&
                kvp.Value.Contains("https", StringComparison.OrdinalIgnoreCase)
            )
            .ToDictionary(
                kvp =>
                {
                    var keyWithoutPrefix = kvp.Key.Substring("Services:".Length).Replace("-", "_");
                    return keyWithoutPrefix.Substring(0, keyWithoutPrefix.Length - 2);
                },
                kvp => kvp.Value
            );

        builder.Services.AddSingleton<Kernel>(serviceProvider =>
        {
            var kernelBuilder = Kernel.CreateBuilder();


            kernelBuilder.Services.AddScoped<IConfiguration>(provider => configuration);

            kernelBuilder.Services.AddOptions<ServiceConfigurationOptions>()
                .Bind(configuration.GetSection("ServiceConfiguration"));

            var serviceConfigurationOptions = configuration.GetSection("ServiceConfiguration").Get<ServiceConfigurationOptions>();

            // We're not currently using CosmosDB in plugins
            //kernelBuilder.Services.AddDbContext<DocGenerationDbContext>(options =>
            //{
            //    options.UseCosmos(connectionString: "docgendb", databaseName: "docgendb");
            //});

            kernelBuilder.Services.AddScoped<IEarthquakeConnector, USGSEarthquakeConnector>();
            kernelBuilder.Services.AddScoped<IMappingConnector, AzureMapsConnector>();
            kernelBuilder.Services.AddScoped<IIndexingProcessor, SearchIndexingProcessor>();

            //Add the openAi Clients from builder.Services to kernelBuilder.Services
            kernelBuilder.Services.AddKeyedScoped<OpenAIClient>("openai-docgen1",(sp, o) => openAiDocGen1);
            kernelBuilder.Services.AddKeyedScoped<OpenAIClient>("openai-planner", (sp, o) => openAiPlanner);

            kernelBuilder.Services.AddKeyedScoped<SearchClient>("searchclient-section",
                (provider, o) => GetSearchClientWithIndex(provider, o, serviceConfigurationOptions.CognitiveSearch.NuclearSectionIndex));
            kernelBuilder.Services.AddKeyedScoped<SearchClient>("searchclient-title",
                (provider, o) => GetSearchClientWithIndex(provider, o, serviceConfigurationOptions.CognitiveSearch.NuclearTitleIndex));
            kernelBuilder.Services.AddKeyedScoped<SearchClient>("searchclient-customdata",
                (provider, o) => GetSearchClientWithIndex(provider, o, serviceConfigurationOptions.CognitiveSearch.CustomIndex));
            
            kernelBuilder.Services.AddAzureOpenAIChatCompletion(serviceConfigurationOptions.OpenAi.PlannerModelDeploymentName, openAiPlanner, "openai-chatcompletion");
            kernelBuilder.Services.AddAzureOpenAITextGeneration(serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName, openAiPlanner, "openai-textgeneration");
            kernelBuilder.Services.AddAzureOpenAITextEmbeddingGeneration("text-embedding-ada002", openAiPlanner, "openai-embeddinggeneration");

            kernelBuilder.Plugins.AddFromType<NuclearDocumentRepositoryPlugin>("native_" + nameof(NuclearDocumentRepositoryPlugin));
            kernelBuilder.Plugins.AddFromType<EarthquakePlugin>("native_" + nameof(EarthquakePlugin));
            kernelBuilder.Plugins.AddFromType<FacilitiesPlugin>("native_" + nameof(FacilitiesPlugin));
            kernelBuilder.Plugins.AddFromType<DatePlugin>("native_" + nameof(DatePlugin));
            kernelBuilder.Plugins.AddFromType<ConversionPlugin>("native_" + nameof(ConversionPlugin));

            var k = kernelBuilder.Build();
            
            foreach (var openApiPlugin in openApiPluginsFromConfiguration)
            {
                if (openApiPlugin.Key.Contains("earthquake")) continue; // temporary to load this as a native plugin instead
                if (k.Plugins.Any(p => p.Name == openApiPlugin.Key)) continue;

                var openApPluginUrlString = openApiPlugin.Value + "/swagger/v1/swagger.json";
                k.ImportPluginFromOpenApiAsync(openApiPlugin.Key, new Uri(openApPluginUrlString),
                    new OpenApiFunctionExecutionParameters()
                    {
                        IgnoreNonCompliantErrors = true,
                    }).GetAwaiter().GetResult();
            }

            return k;

            SearchClient GetSearchClientWithIndex(IServiceProvider serviceProvider, object? key, string indexName)
            {
                var searchClient = new SearchClient(
                    new Uri(serviceConfigurationOptions.CognitiveSearch.Endpoint),
                    indexName,
                    new AzureKeyCredential(serviceConfigurationOptions.CognitiveSearch.Key));
                return searchClient;
            }
        });


        return builder;
    }
}

