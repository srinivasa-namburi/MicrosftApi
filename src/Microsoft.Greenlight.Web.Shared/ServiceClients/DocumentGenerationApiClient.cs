using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public class DocumentGenerationApiClient : CrossPlatformServiceClientBase<DocumentGenerationApiClient>, IDocumentGenerationApiClient
{
    // Single DI-friendly constructor to avoid ambiguity with typed HttpClient
    public DocumentGenerationApiClient(HttpClient httpClient, ILogger<DocumentGenerationApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<string?> GenerateDocumentAsync(GenerateDocumentDTO? generateDocumentDto)
    {
        if (generateDocumentDto == null)
        {
            throw new ArgumentNullException(nameof(generateDocumentDto));
        }

        var response = await SendPostRequestMessage($"/api/documents/generate", generateDocumentDto, authorize: true);
        response?.EnsureSuccessStatusCode();
        return response?.StatusCode.ToString();
    }

    public async Task<GeneratedDocumentInfo?> GetDocumentAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<GeneratedDocumentInfo>()! ??
               throw new IOException("No document!");
    }

    public async Task<GeneratedDocumentInfo?> GetDocumentHeaderAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}/header", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<GeneratedDocumentInfo>()! ??
               throw new IOException("No document header!");
    }

    public async Task<bool> DeleteGeneratedDocumentAsync(string documentId)
    {
        var response = await SendDeleteRequestMessage($"/api/documents/{documentId}", authorize: true);
        response?.EnsureSuccessStatusCode();
        return response?.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<Stream> ExportDocumentAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}/word-export", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadAsStreamAsync()!;
    }

    public async Task<string?> GenerateExportDocumentLinkAsync(string? documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}/word-export/permalink", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadAsStringAsync()!;
    }

    public async Task<string> GetExportDocumentLinkAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}/export-link", authorize: true);
        if (response?.IsSuccessStatusCode == true)
        {
            return await response!.Content.ReadAsStringAsync()!;
        }
        else
        {
            return string.Empty;
        }
    }

    public async Task<List<GeneratedDocumentListItem>> GetGeneratedDocumentsAsync()
    {
        var response = await SendGetRequestMessage($"/api/documents", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<GeneratedDocumentListItem>>()! ??
               throw new IOException("No documents!");
    }

    public async Task<bool> StartDocumentValidationAsync(string documentId)
    {
        try
        {
            var response = await SendPostRequestMessage($"/api/document-validation/{documentId}", null, authorize: true);
            response?.EnsureSuccessStatusCode();
            return response?.IsSuccessStatusCode ?? false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start validation for document {DocumentId}", documentId);
            return false;
        }
    }

    public async Task<DocumentGenerationStatusInfo?> GetDocumentGenerationStatusAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}/status", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<DocumentGenerationStatusInfo>()!;
    }

    public async Task<DocumentGenerationFullStatusInfo?> GetDocumentGenerationFullStatusAsync(string documentId)
    {
        var response = await SendGetRequestMessage($"/api/documents/{documentId}/status/full", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<DocumentGenerationFullStatusInfo>()!;
    }
}
