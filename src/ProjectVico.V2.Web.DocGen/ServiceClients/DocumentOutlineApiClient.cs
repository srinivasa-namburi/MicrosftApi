using System.Net;
using Microsoft.AspNetCore.Components.Authorization;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

public class DocumentOutlineApiClient : BaseServiceClient<DocumentOutlineApiClient>, IDocumentOutlineApiClient
{
    public DocumentOutlineApiClient(HttpClient httpClient, ILogger<DocumentOutlineApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
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
        var url = $"/api/document-outline/{documentOutlineChangeRequest.DocumentOutlineId}/changes";
        var response = await SendPostRequestMessage(url, documentOutlineChangeRequest);

        if (response!.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<DocumentOutlineInfo>()!;
    }
}