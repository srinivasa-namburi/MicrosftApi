using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Factory class for creating instances of SearchClient and SearchIndexClient.
/// </summary>
public class SearchClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly SearchIndexClient _baseSearchIndexClient;
    private readonly AzureCredentialHelper _azureCredentialHelper;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    private Dictionary<string, SearchClient>? searchClients;
    private Dictionary<string, SearchIndexClient>? searchIndexClients;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchClientFactory"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="baseSearchIndexClient">The base search index client.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <param name="azureCredentialHelper">The Azure credential helper.</param>
    public SearchClientFactory(
        IConfiguration configuration,
        SearchIndexClient baseSearchIndexClient,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        AzureCredentialHelper azureCredentialHelper
        )
    {
        _configuration = configuration;
        _baseSearchIndexClient = baseSearchIndexClient;
        _azureCredentialHelper = azureCredentialHelper;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }

    /// <summary>
    /// Gets the search index client for the specified index name.
    /// </summary>
    /// <param name="indexName">Name of the index.</param>
    /// <returns>The search index client.</returns>
    public virtual SearchIndexClient GetSearchIndexClientForIndex(string indexName)
    {
        searchIndexClients ??= new Dictionary<string, SearchIndexClient>();

        if (!searchIndexClients.ContainsKey(indexName))
        {
            var searchIndexClient = new SearchIndexClient(
                new Uri(_baseSearchIndexClient.Endpoint.AbsoluteUri),
                _azureCredentialHelper.GetAzureCredential()
            );

            searchIndexClients.Add(indexName, searchIndexClient);
        }

        return searchIndexClients[indexName];
    }

    /// <summary>
    /// Gets the search client for the specified index name.
    /// </summary>
    /// <param name="indexName">Name of the index.</param>
    /// <returns>The search client.</returns>
    public virtual SearchClient GetSearchClientForIndex(string indexName)
    {
        searchClients ??= new Dictionary<string, SearchClient>();

        if (!searchClients.ContainsKey(indexName))
        {
            var searchClient = new SearchClient(
                new Uri(_baseSearchIndexClient.Endpoint.AbsoluteUri),
                indexName,
                _azureCredentialHelper.GetAzureCredential()
            );

            searchClients.Add(indexName, searchClient);
        }

        return searchClients[indexName];
    }
}
