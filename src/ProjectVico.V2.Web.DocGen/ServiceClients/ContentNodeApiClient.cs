using Microsoft.AspNetCore.Components.Authorization;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

internal sealed class ContentNodeApiClient : BaseServiceClient<ContentNodeApiClient>, IContentNodeApiClient
{
    public ContentNodeApiClient(HttpClient httpClient, AuthenticationStateProvider asp, ILogger<ContentNodeApiClient> logger) : base(httpClient, asp, logger)
    {
    }

    public async Task<ContentNode?> GetContentNodeAsync(string contentNodeId)
    {
        var response = await SendGetRequestMessage($"/api/content-nodes/{contentNodeId}");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<ContentNode>()! ??
               throw new IOException("No content node!");
    }
}