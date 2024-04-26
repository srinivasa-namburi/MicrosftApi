using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.Shared.Services.Search;

public class SearchClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly SearchIndexClient _baseSearchIndexClient;
    private ServiceConfigurationOptions _serviceConfigurationOptions;

    private Dictionary<string, SearchClient>? searchClients;
    private Dictionary<string, SearchIndexClient>? searchIndexClients;

    public SearchClientFactory(
        IConfiguration configuration,
        SearchIndexClient baseSearchIndexClient,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions
        )
    {
        _configuration = configuration;
        _baseSearchIndexClient = baseSearchIndexClient;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }

    public SearchIndexClient GetSearchIndexClientForIndex(string indexName)
    {
        searchIndexClients ??= new Dictionary<string, SearchIndexClient>();

        if (!searchIndexClients.ContainsKey(indexName))
        {
            var searchIndexClient = new SearchIndexClient(
                new Uri(_baseSearchIndexClient.Endpoint.AbsoluteUri),
                new DefaultAzureCredential()
            );

            searchIndexClients.Add(indexName, searchIndexClient);
        }

        return searchIndexClients[indexName];
    }

    public SearchClient GetSearchClientForIndex(string indexName)
    {
        searchClients ??= new Dictionary<string, SearchClient>();

        if (!searchClients.ContainsKey(indexName))
        {
            var searchClient = new SearchClient(
                new Uri(_baseSearchIndexClient.Endpoint.AbsoluteUri),
                indexName,
                new DefaultAzureCredential()
            );

            searchClients.Add(indexName, searchClient);
        }

        return searchClients[indexName];
    }
}