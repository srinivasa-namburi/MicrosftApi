using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Search;

namespace ProjectVico.V2.Worker.DocumentIngestion.AI;


public static class SemanticKernelExtensions
{
    public static IHostApplicationBuilder AddSemanticKernelService(this IHostApplicationBuilder builder)
    {

        var sp = builder.Services.BuildServiceProvider();

        var openAiPlanner = sp.GetKeyedService<OpenAIClient>("openai-planner");
       
        var openAiDocGen1 = sp.GetKeyedService<OpenAIClient>("openai-docgen1");

        var configuration = builder.Configuration;
        
        builder.Services.AddSingleton<Kernel>(serviceProvider =>
        {
            var kernelBuilder = Kernel.CreateBuilder();


            kernelBuilder.Services.AddScoped<IConfiguration>(provider => configuration);

            kernelBuilder.Services.AddOptions<ServiceConfigurationOptions>()
                .Bind(configuration.GetSection("ServiceConfiguration"));

            var serviceConfigurationOptions = configuration.GetSection("ServiceConfiguration").Get<ServiceConfigurationOptions>();

            kernelBuilder.Services.AddScoped<IIndexingProcessor, SearchIndexingProcessor>();
            kernelBuilder.Services.AddScoped<TableHelper>();

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
            
            var k = kernelBuilder.Build();
           
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

