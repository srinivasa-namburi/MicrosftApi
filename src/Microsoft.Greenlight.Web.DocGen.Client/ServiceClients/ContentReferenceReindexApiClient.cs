using Microsoft.Greenlight.Grains.Shared.Contracts.State;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net.Http.Json;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class ContentReferenceReindexApiClient : WebAssemblyBaseServiceClient<ContentReferenceReindexApiClient>, IContentReferenceReindexApiClient
{
    public ContentReferenceReindexApiClient(HttpClient httpClient, ILogger<ContentReferenceReindexApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task StartAsync(ContentReferenceType type, string reason)
    {
        var response = await SendPostRequestMessage($"/api/content-reference-reindex/{type}", reason);
        response?.EnsureSuccessStatusCode();
    }

    public async Task<ContentReferenceReindexState?> GetStateAsync(ContentReferenceType type)
    {
        var response = await SendGetRequestMessage($"/api/content-reference-reindex/{type}/state");
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<ContentReferenceReindexState>();
    }
}
