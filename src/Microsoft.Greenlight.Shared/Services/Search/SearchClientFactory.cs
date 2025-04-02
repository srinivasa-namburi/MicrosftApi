using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Factory class for creating instances of SearchClient and SearchIndexClient.
/// </summary>
public sealed class SearchClientFactory
{
    private readonly SearchIndexClient _baseSearchIndexClient;
    private readonly AzureCredentialHelper _azureCredentialHelper;

    private Dictionary<string, SearchClient>? _searchClients = [];
    private Dictionary<string, SearchIndexClient>? _searchIndexClients = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchClientFactory"/> class.
    /// </summary>
    /// <param name="baseSearchIndexClient">The base search index client.</param>
    /// <param name="azureCredentialHelper">The Azure credential helper.</param>
    public SearchClientFactory(
        SearchIndexClient baseSearchIndexClient,
        AzureCredentialHelper azureCredentialHelper
        )
    {
        _baseSearchIndexClient = baseSearchIndexClient;
        _azureCredentialHelper = azureCredentialHelper;
    }

    /// <summary>
    /// Gets the search index client for the specified index name.
    /// </summary>
    /// <param name="indexName">Name of the index.</param>
    /// <returns>The search index client.</returns>
    public SearchIndexClient GetSearchIndexClientForIndex(string indexName)
    {
        _searchIndexClients ??= new Dictionary<string, SearchIndexClient>();

        if (!_searchIndexClients.ContainsKey(indexName))
        {
            var searchIndexClient = new SearchIndexClient(
                new Uri(_baseSearchIndexClient.Endpoint.AbsoluteUri),
                _azureCredentialHelper.GetAzureCredential()
            );

            _searchIndexClients.Add(indexName, searchIndexClient);
        }

        return _searchIndexClients[indexName];
    }

    /// <summary>
    /// Gets the search client for the specified index name.
    /// </summary>
    /// <param name="indexName">Name of the index.</param>
    /// <returns>The search client.</returns>
    public SearchClient GetSearchClientForIndex(string indexName)
    {
        _searchClients ??= new Dictionary<string, SearchClient>();

        if (!_searchClients.ContainsKey(indexName))
        {
            var searchClient = new SearchClient(
                new Uri(_baseSearchIndexClient.Endpoint.AbsoluteUri),
                indexName,
                _azureCredentialHelper.GetAzureCredential()
            );

            _searchClients.Add(indexName, searchClient);
        }

        return _searchClients[indexName];
    }
}
