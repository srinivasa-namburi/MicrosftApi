using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net.Http.Json;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients
{
    public class ContentReferenceApiClient : WebAssemblyBaseServiceClient<ContentReferenceApiClient>, IContentReferenceApiClient
    {
        public ContentReferenceApiClient(HttpClient httpClient, ILogger<ContentReferenceApiClient> logger, AuthenticationStateProvider authStateProvider)
            : base(httpClient, logger, authStateProvider)
        {
        }

        public async Task<List<ContentReferenceItemInfo>> GetAllReferencesAsync()
        {
            var response = await SendGetRequestMessage("/api/content-references/all");
            return await response?.Content.ReadFromJsonAsync<List<ContentReferenceItemInfo>>()! ?? new List<ContentReferenceItemInfo>();
        }

        public async Task<List<ContentReferenceItemInfo>> SearchReferencesAsync(string term)
        {
            var response = await SendGetRequestMessage($"/api/content-references/search?term={term}");
            return await response?.Content.ReadFromJsonAsync<List<ContentReferenceItemInfo>>()! ?? new List<ContentReferenceItemInfo>();
        }

        public async Task<ContentReferenceItemInfo> GetReferenceByIdAsync(Guid id, ContentReferenceType type)
        {
            var response = await SendGetRequestMessage($"/api/content-references/{id}/{type}");
            return await response?.Content.ReadFromJsonAsync<ContentReferenceItemInfo>()! ?? new ContentReferenceItemInfo();
        }

        public async Task RefreshReferenceCacheAsync()
        {
            var response = await SendPostRequestMessage("/api/content-references/refresh", null);
            response?.EnsureSuccessStatusCode();
        }

        public async Task<bool> RemoveReferenceAsync(Guid referenceId, Guid conversationId)
        {
            var response = await SendDeleteRequestMessage($"/api/content-references/remove/{referenceId}/{conversationId}");
            // If controller returns 404, return false; otherwise ensure success.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            response.EnsureSuccessStatusCode();
            return true;
        }
    }
}