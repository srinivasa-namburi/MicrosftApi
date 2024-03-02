using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

internal sealed class ContentNodeApiClient : BaseServiceClient<ContentNodeApiClient>, IContentNodeApiClient
{
    public ContentNodeApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor,
        ILogger<ContentNodeApiClient> logger) : base(
        httpClient, httpContextAccessor, logger)
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