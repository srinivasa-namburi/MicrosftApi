using System.Net;
using System.Text.Json;
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

        if (documentGenerationRequest == null)
        {
            throw new ArgumentNullException(nameof(documentGenerationRequest));
        }

        var generateDocumentDto = new GenerateDocumentDTO
        {
            DocumentProcessName = documentGenerationRequest.DocumentProcessName,
            DocumentTitle = documentGenerationRequest.DocumentTitle,
            AuthorOid = documentGenerationRequest.AuthorOid,
            Id = documentGenerationRequest.Id,
            RequestAsJson = JsonSerializer.Serialize(documentGenerationRequest)
        };

        var response = await SendPostRequestMessage($"/api/documents/generate", generateDocumentDto);
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

    public async Task<bool> DeleteGeneratedDocumentAsync(string documentId)
    {
        var response = await SendDeleteRequestMessage($"/api/documents/{documentId}");
        response?.EnsureSuccessStatusCode();

        return response?.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<List<GeneratedDocumentListItem>> GetGeneratedDocumentsAsync()
    {
        var response = await SendGetRequestMessage($"/api/documents");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<List<GeneratedDocumentListItem>>()! ??
               throw new IOException("No documents!");
    }
}