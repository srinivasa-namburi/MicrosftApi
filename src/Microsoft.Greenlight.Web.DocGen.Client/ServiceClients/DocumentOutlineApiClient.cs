using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class DocumentOutlineApiClient : WebAssemblyBaseServiceClient<DocumentOutlineApiClient>, IDocumentOutlineApiClient
{
    public DocumentOutlineApiClient(HttpClient httpClient, ILogger<DocumentOutlineApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<List<DocumentOutlineItemInfo>> GenerateOutlineFromTextAsync(string outlineText)
    {
        var simpleText = new SimpleTextDTO()
        {
            Text = outlineText
        };
        var url = "/api/document-outline/generate-from-text";
        var response = await SendPostRequestMessage(url, simpleText);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<List<DocumentOutlineItemInfo>>();
            return result ?? new List<DocumentOutlineItemInfo>();
        }
        else
        {
            // Handle error response
            return new List<DocumentOutlineItemInfo>();
        }
    }

    public async Task<List<DocumentOutlineInfo>> GetDocumentOutlinesAsync()
    {
        var url = "/api/document-outlines";
        var response = await SendGetRequestMessage(url);
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<List<DocumentOutlineInfo>>()! ??
               throw new IOException("No document outlines!");
    }

    public async Task<DocumentOutlineInfo?> GetDocumentOutlineByIdAsync(Guid id)
    {
        var url = $"/api/document-outline/{id}";
        var response = await SendGetRequestMessage(url);

        if (response!.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<DocumentOutlineInfo>()!;
    }

    public async Task<DocumentOutlineInfo?> CreateDocumentOutlineAsync(DocumentOutlineInfo documentOutlineInfo)
    {
        var url = "/api/document-outline";
        var response = await SendPostRequestMessage(url, documentOutlineInfo);
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<DocumentOutlineInfo>()!;
    }

    public async Task<DocumentOutlineInfo?> UpdateDocumentOutlineAsync(Guid id,
        DocumentOutlineChangeRequest documentOutlineChangeRequest)
    {
        var url = $"/api/document-outline/{id}/changes";
        var response = await SendPostRequestMessage(url, documentOutlineChangeRequest);

        if (response!.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<DocumentOutlineInfo>()!;
    }
}
