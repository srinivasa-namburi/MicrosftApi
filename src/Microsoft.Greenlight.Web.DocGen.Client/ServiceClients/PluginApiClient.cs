using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

internal sealed class PluginApiClient : WebAssemblyBaseServiceClient<PluginApiClient>, IPluginApiClient
{
    public PluginApiClient(HttpClient httpClient, ILogger<PluginApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<List<DynamicPluginInfo>> GetAllPluginsAsync()
    {
        var url = "/api/plugins";
        var response = await SendGetRequestMessage(url);
        response?.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DynamicPluginInfo>>();
    }

    public async Task<DynamicPluginInfo> GetPluginByIdAsync(Guid pluginId)
    {
        var url = $"/api/plugins/{pluginId}";
        var response = await SendGetRequestMessage(url);
        response?.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DynamicPluginInfo>();
    }
    public async Task<List<DynamicPluginInfo>> GetPluginsByDocumentProcessIdAsync(Guid documentProcessId)
    {
        var url = $"/api/document-processes/{documentProcessId}/plugins";
        var response = await SendGetRequestMessage(url);
        response?.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DynamicPluginInfo>>();
    }

    public async Task AssociatePluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, DynamicPluginVersionInfo version)
    {
        var url = $"/api/plugins/{pluginId}/{version.ToString()}/associate/{documentProcessId}";
        var response = await SendPostRequestMessage(url, payload: null);
        response?.EnsureSuccessStatusCode();
    }

    public async Task DisassociatePluginFromDocumentProcessAsync(Guid pluginId, Guid documentProcessId)
    {
        var url = $"/api/plugins/{pluginId}/disassociate/{documentProcessId}";
        var response = await SendPostRequestMessage(url, payload: null);
        response?.EnsureSuccessStatusCode();
    }

    public async Task<DynamicPluginInfo> UploadPluginAsync(IBrowserFile pluginFile)
    {
        var url = "/api/plugins/upload";
        var response = await SendPostRequestMessage(url, pluginFile);

        return await response.Content.ReadFromJsonAsync<DynamicPluginInfo>();
    }
}