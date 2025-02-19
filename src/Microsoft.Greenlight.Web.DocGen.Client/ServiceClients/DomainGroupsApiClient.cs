using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients
{
    public class DomainGroupsApiClient : WebAssemblyBaseServiceClient<DomainGroupsApiClient>, IDomainGroupsApiClient
    {
        public DomainGroupsApiClient(HttpClient httpClient, ILogger<DomainGroupsApiClient> logger, AuthenticationStateProvider authStateProvider)
            : base(httpClient, logger, authStateProvider)
        {
        }

        public async Task<DomainGroupInfo?> GetDomainGroupAsync(Guid domainGroupId)
        {
            var response = await SendGetRequestMessage($"/api/domain-groups/{domainGroupId}");
            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<DomainGroupInfo>()!;
        }

        public async Task<List<DomainGroupInfo>> GetDomainGroupsAsync()
        {
            var response = await SendGetRequestMessage("/api/domain-groups");
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<List<DomainGroupInfo>>()! ?? new List<DomainGroupInfo>();
        }

        public async Task<DomainGroupInfo> CreateDomainGroupAsync(DomainGroupInfo domainGroup)
        {
            var response = await SendPostRequestMessage("/api/domain-groups", domainGroup);
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<DomainGroupInfo>()!;
        }

        public async Task<DomainGroupInfo> UpdateDomainGroupAsync(DomainGroupInfo domainGroup)
        {
            var response = await SendPutRequestMessage($"/api/domain-groups/{domainGroup.Id}", domainGroup);
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<DomainGroupInfo>()!;
        }

        public async Task<bool> DeleteDomainGroupAsync(Guid domainGroupId)
        {
            var response = await SendDeleteRequestMessage($"/api/domain-groups/{domainGroupId}");
            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
            response?.EnsureSuccessStatusCode();
            return true;
        }

        public async Task<DomainGroupInfo?> AssociateDocumentProcessAsync(Guid domainGroupId, Guid documentProcessId)
        {
            var response = await SendPostRequestMessage($"/api/domain-groups/{domainGroupId}/document-processes/{documentProcessId}", null);
            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<DomainGroupInfo>()!;
        }

        public async Task<DomainGroupInfo?> DisassociateDocumentProcessAsync(Guid domainGroupId, Guid documentProcessId)
        {
            var response = await SendDeleteRequestMessage($"/api/domain-groups/{domainGroupId}/document-processes/{documentProcessId}");
            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<DomainGroupInfo>()!;
        }
    }
}
