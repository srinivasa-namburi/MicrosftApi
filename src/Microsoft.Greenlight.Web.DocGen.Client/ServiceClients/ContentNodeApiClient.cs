using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class ContentNodeApiClient : WebAssemblyBaseServiceClient<ContentNodeApiClient>, IContentNodeApiClient
{
    public ContentNodeApiClient(HttpClient httpClient, ILogger<ContentNodeApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<ContentNodeInfo?> GetContentNodeAsync(string contentNodeId)
    {
        var response = await SendGetRequestMessage($"/api/content-nodes/{contentNodeId}");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<ContentNodeInfo>()! ??
               throw new IOException("No content node!");
    }

    public async Task<ContentNodeSystemItemInfo?> GetContentNodeSystemItemAsync(Guid contentNodeSystemItemId)
    {
        var response = await SendGetRequestMessage($"/api/content-nodes/content-node-system-item/{contentNodeSystemItemId}");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<ContentNodeSystemItemInfo>()! ??
               throw new IOException("No content node system item!");
    }
}
