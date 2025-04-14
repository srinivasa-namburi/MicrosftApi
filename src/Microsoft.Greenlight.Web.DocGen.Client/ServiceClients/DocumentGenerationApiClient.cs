using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class DocumentGenerationApiClient : WebAssemblyBaseServiceClient<DocumentGenerationApiClient>, IDocumentGenerationApiClient
{
    public DocumentGenerationApiClient(HttpClient httpClient, ILogger<DocumentGenerationApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<string?> GenerateDocumentAsync(GenerateDocumentDTO? generateDocumentDto)
    {
        if (generateDocumentDto == null)
        {
            throw new ArgumentNullException(nameof(generateDocumentDto));
        }

        var response = await SendPostRequestMessage($"/api/documents/generate", generateDocumentDto);
        response?.EnsureSuccessStatusCode();

        return response?.StatusCode.ToString();
    }

    public async Task<GeneratedDocumentInfo?> GetDocumentAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<GeneratedDocumentInfo>()! ??
               throw new IOException("No document!");
    }

    public async Task<bool> DeleteGeneratedDocumentAsync(string documentId)
    {
        var response = await SendDeleteRequestMessage($"/api/documents/{documentId}");
        response?.EnsureSuccessStatusCode();

        return response?.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<Stream> ExportDocumentAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}/word-export");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadAsStreamAsync()!;
    }

    public async Task<string?> GenerateExportDocumentLinkAsync(string? documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}/word-export/permalink");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadAsStringAsync()!;
    }

    public async Task<string> GetExportDocumentLinkAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}/export-link");
        
        if(response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync()!;
        }
        else
        {
            return string.Empty;
        }
    }

    public async Task<List<GeneratedDocumentListItem>> GetGeneratedDocumentsAsync()
    {
        var response = await SendGetRequestMessage($"/api/documents");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<List<GeneratedDocumentListItem>>()! ??
               throw new IOException("No documents!");
    }

    public async Task<bool> StartDocumentValidationAsync(string documentId)
    {
        try
        {
            var response = await SendPostRequestMessage($"/api/document-validation/{documentId}", null);
            response?.EnsureSuccessStatusCode();
        
            return response?.IsSuccessStatusCode ?? false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start validation for document {DocumentId}", documentId);
            return false;
        }
    }
}
