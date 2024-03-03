using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

internal sealed class DocumentGenerationApiClient : BaseServiceClient<DocumentGenerationApiClient>, IDocumentGenerationApiClient
{
    public DocumentGenerationApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, ILogger<DocumentGenerationApiClient> logger) : base(
        httpClient, httpContextAccessor, logger)
    {
    }

    public async Task<string?> GenerateDocumentAsync(DocumentGenerationRequest? documentGenerationRequest)
    {
        var response = await SendPostRequestMessage($"/api/documents/generate", documentGenerationRequest);
        response?.EnsureSuccessStatusCode();

        return response?.StatusCode.ToString();
    }

    public async Task<GeneratedDocument?> GetDocumentAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<GeneratedDocument>()! ??
               throw new IOException("No document!");
    }

    public async Task<List<GeneratedDocumentListItem>> GetGeneratedDocumentsAsync()
    {
        var response = await SendGetRequestMessage($"/api/documents");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<List<GeneratedDocumentListItem>>()! ??
               throw new IOException("No documents!");
    }
}