using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

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

    public async Task<List<ContentNodeVersion>> GetContentNodeVersionsAsync(Guid contentNodeId)
    {
        var response = await SendGetRequestMessage($"/api/content-nodes/{contentNodeId}/versions");
        response?.EnsureSuccessStatusCode();
        
        return await response?.Content.ReadFromJsonAsync<List<ContentNodeVersion>>() ?? [];
    }
    
    public async Task<bool> HasPreviousVersionsAsync(Guid contentNodeId)
    {
        var response = await SendGetRequestMessage($"/api/content-nodes/{contentNodeId}/has-versions");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<bool>();
    }
    
    public async Task<ContentNodeInfo?> UpdateContentNodeTextAsync(Guid contentNodeId, string newText, ContentNodeVersioningReason reason, string? comment = null)
    {
        var request = new
        {
            NewText = newText,
            VersioningReason = reason,
            Comment = comment
        };
    
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // This helps with special characters
        };
    
        var content = new StringContent(
            JsonSerializer.Serialize(request, jsonOptions),
            Encoding.UTF8,
            "application/json");
    
        var response = await SendPutRequestMessage($"/api/content-nodes/{contentNodeId}/text", request);
        response?.EnsureSuccessStatusCode();
    
        return await response?.Content.ReadFromJsonAsync<ContentNodeInfo>(jsonOptions);
    }
    
    public async Task<ContentNodeInfo?> PromoteContentNodeVersionAsync(Guid contentNodeId, Guid versionId, string? comment = null)
    {
        var request = new
        {
            VersionId = versionId,
            Comment = comment
        };
        
        var response = await SendPutRequestMessage($"/api/content-nodes/{contentNodeId}/promote-version", request);
        response?.EnsureSuccessStatusCode();
        
        return await response?.Content.ReadFromJsonAsync<ContentNodeInfo>()!;
    }
}
