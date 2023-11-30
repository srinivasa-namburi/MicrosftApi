using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.Backend.DocumentIngestion.Shared;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;
using ProjectVico.Plugins.Shared.Extensions;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureHostConfiguration(x => x.BuildPluginConfigurationBuilder());


hostBuilder.ConfigureServices((hostContext, services) =>
{
    services.Configure<AiOptions>(hostContext.Configuration.GetSection("AI"));

    var aiOptions = hostContext.Configuration.GetSection("AI").Get<AiOptions>();

    services.AddScoped<OpenAIClient>((serviceProvider) => new OpenAIClient(
        new Uri(aiOptions.OpenAI.Endpoint),
        new AzureKeyCredential(aiOptions.OpenAI.Key)));

    services.AddKeyedScoped<SearchClient>("searchclient-section",
        (provider, o) => GetSearchClientWithIndex(provider, o, aiOptions.CognitiveSearch.SectionIndex));
    services.AddKeyedScoped<SearchClient>("searchclient-title",
        (provider, o) => GetSearchClientWithIndex(provider, o, aiOptions.CognitiveSearch.TitleIndex));

    services.AddScoped<IIndexingProcessor, SearchIndexingProcessor>();

    SearchClient GetSearchClientWithIndex(IServiceProvider serviceProvider, object? key, string indexName)
    {
        var searchClient = new SearchClient(
            new Uri(aiOptions.CognitiveSearch.Endpoint),
            indexName,
            new AzureKeyCredential(aiOptions.CognitiveSearch.Key));
        return searchClient;
    }

});

var host = hostBuilder.Build();
host.Run();
