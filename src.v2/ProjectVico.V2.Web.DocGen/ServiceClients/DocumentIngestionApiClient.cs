using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

internal sealed class DocumentIngestionApiClient : BaseServiceClient<DocumentIngestionApiClient>, IDocumentIngestionApiClient
{
    public DocumentIngestionApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor,
        ILogger<DocumentIngestionApiClient> logger) : base(
        httpClient, httpContextAccessor, logger)
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