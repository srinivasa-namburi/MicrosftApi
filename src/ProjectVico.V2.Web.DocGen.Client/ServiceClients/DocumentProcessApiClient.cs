using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Contracts.Chat;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.Client.ServiceClients;

public class DocumentProcessApiClient : WebAssemblyBaseServiceClient<DocumentProcessApiClient>, IDocumentProcessApiClient
{
    public DocumentProcessApiClient(HttpClient httpClient, ILogger<DocumentProcessApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<List<DocumentProcessInfo>> GetAllDocumentProcessInfoAsync()
    {
        var url = "/api/document-process";
        var response = await SendGetRequestMessage(url);

        // If we get a 404, it means that no document processes exist - return an empty list
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response?.EnsureSuccessStatusCode();

        // Return all DocumentInfo found
        var result = await response?.Content.ReadFromJsonAsync<List<DocumentProcessInfo>>()! ?? [];
        return result;
    }

    public async Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName)
    {
        var url = $"/api/document-process/short-name/{shortName}";
        var response = await SendGetRequestMessage(url);

        // If we get a 404, it means that the document process does not exist - return null
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();

        // Return the DocumentInfo if found - otherwise return null
        var result = await response?.Content.ReadFromJsonAsync<DocumentProcessInfo>()! ?? null;
        return result;
    }

    public async Task<DocumentProcessInfo?> CreateDynamicDocumentProcessDefinitionAsync(DocumentProcessInfo documentProcessInfo)
    {
        var url = "/api/document-process";
        var response = await SendPostRequestMessage(url, documentProcessInfo);
        
        response?.EnsureSuccessStatusCode();

        // Return the created DocumentInfo
        var result = await response?.Content.ReadFromJsonAsync<DocumentProcessInfo>()! ?? null;
        return result;
    }
}