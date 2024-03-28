using Microsoft.AspNetCore.Components.Authorization;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

internal sealed class DocumentIngestionApiClient : BaseServiceClient<DocumentIngestionApiClient>, IDocumentIngestionApiClient
{
    public DocumentIngestionApiClient(HttpClient httpClient, ILogger<DocumentIngestionApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }
    
    public async Task<string?> IngestDocumentAsync(DocumentIngestionRequest? documentIngestionRequest)
    {
        var response = await SendPostRequestMessage($"/api/documents/ingest", documentIngestionRequest);
        response?.EnsureSuccessStatusCode();

        return response?.StatusCode.ToString();
    }

    public async Task<string?> ReindexAllDocuments()
    {
        var response = await SendPostRequestMessage($"/api/documents/reindex-all", null);
        response?.EnsureSuccessStatusCode();
        return response?.StatusCode.ToString();

    }
}