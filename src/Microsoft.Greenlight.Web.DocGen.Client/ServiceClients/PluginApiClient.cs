using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using System.Net;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

internal sealed class PluginApiClient : WebAssemblyBaseServiceClient<PluginApiClient>, IPluginApiClient
{
    public PluginApiClient(HttpClient httpClient, ILogger<PluginApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<List<McpPluginInfo>> GetAllMcpPluginsAsync()
    {
        try
        {
            var url = "/api/mcp-plugins";
            var response = await SendGetRequestMessage(url);

            // If not found, just return an empty list rather than throwing
            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<McpPluginInfo>();
            }

            response?.EnsureSuccessStatusCode();
            return await response!.Content.ReadFromJsonAsync<List<McpPluginInfo>>() ?? new List<McpPluginInfo>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting all MCP plugins");
            return new List<McpPluginInfo>();
        }
    }

    public async Task<McpPluginInfo> GetMcpPluginByIdAsync(Guid pluginId)
    {
        try
        {
            // First fetch the plugin without full document process details to avoid circular references
            var url = $"/api/mcp-plugins/{pluginId}";
            var response = await SendGetRequestMessage(url);
            response?.EnsureSuccessStatusCode();
            
            var plugin = await response!.Content.ReadFromJsonAsync<McpPluginInfo>();
            
            if (plugin == null)
            {
                throw new InvalidOperationException($"Failed to deserialize plugin with ID: {pluginId}");
            }
            
            // Now separately fetch document processes associated with this plugin
            if (plugin.DocumentProcesses != null)
            {
                var documentProcesses = await GetDocumentProcessesByPluginIdAsync(pluginId);
                
                // Update just the necessary DocumentProcess information without creating circular references
                foreach (var association in plugin.DocumentProcesses)
                {
                    var documentProcess = documentProcesses.FirstOrDefault(dp => dp.Id == association.DocumentProcessId);
                    if (documentProcess != null)
                    {
                        association.DocumentProcess = documentProcess;
                        // Important: Do not set DocumentProcess.Plugin to avoid circular references
                    }
                }
            }
            
            return plugin;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error getting MCP plugin by ID {pluginId}");
            throw;
        }
    }

    public async Task<List<McpPluginInfo>> GetMcpPluginsByDocumentProcessIdAsync(Guid documentProcessId)
    {
        try
        {
            var url = $"/api/document-processes/{documentProcessId}/mcp-plugins";
            var response = await SendGetRequestMessage(url);

            // If not found, just return an empty list rather than throwing
            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<McpPluginInfo>();
            }

            response?.EnsureSuccessStatusCode();
            return await response!.Content.ReadFromJsonAsync<List<McpPluginInfo>>() ?? new List<McpPluginInfo>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error getting MCP plugins for document process {documentProcessId}");
            return new List<McpPluginInfo>();
        }
    }

    public async Task AssociateMcpPluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, McpPluginVersionInfo version, bool keepOnLatestVersion = false)
    {
        var url = $"/api/mcp-plugins/{pluginId}/{version.Major}.{version.Minor}.{version.Patch}/associate/{documentProcessId}?keepOnLatestVersion={keepOnLatestVersion.ToString().ToLower()}";
        var response = await SendPostRequestMessage(url, payload: null);
        response?.EnsureSuccessStatusCode();
    }

    public async Task DisassociateMcpPluginFromDocumentProcessAsync(Guid pluginId, Guid documentProcessId)
    {
        var url = $"/api/mcp-plugins/{pluginId}/disassociate/{documentProcessId}";
        var response = await SendPostRequestMessage(url, payload: null);
        response?.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Uploads an MCP plugin and checks if overrides are needed.
    /// </summary>
    /// <param name="pluginFile">The plugin file to upload.</param>
    /// <returns>A tuple containing the plugin info and a flag indicating if overrides are needed.</returns>
    public async Task<(McpPluginInfo PluginInfo, bool NeedsOverride)> UploadMcpPluginAsyncWithOverrideCheck(IBrowserFile pluginFile)
    {
        var url = "/api/mcp-plugins/upload";
        var response = await SendPostRequestMessage(url, pluginFile);

        if (response!.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new HttpRequestException(await response.Content.ReadAsStringAsync());
        }

        response?.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<McpPluginUploadResponse>();

        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize the response from the server.");
        }

        return (result.PluginInfo, result.NeedsOverride);
    }

    public async Task UpdateMcpPluginAssociationAsync(Guid documentProcessId, Guid pluginId, McpPluginAssociationInfo update)
    {
        var url = $"/api/document-processes/{documentProcessId}/mcp-plugins/{pluginId}/association";
        var response = await SendPutRequestMessage(url, update);
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Association not found between plugin and document process.");
        }
        response?.EnsureSuccessStatusCode();
    }

    public async Task<McpPluginInfo> CreateCommandOnlyMcpPluginAsync(CommandOnlyMcpPluginCreateModel createModel)
    {
        var url = "/api/mcp-plugins/command-only";
        var response = await SendPostRequestMessage(url, createModel);
        
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException($"API endpoint not found: {url}");
        }
        
        if (response?.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new HttpRequestException($"{await response.Content.ReadAsStringAsync()}");
        }
        
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<McpPluginInfo>();
    }
    
    public async Task<McpPluginInfo> CreateSseMcpPluginAsync(SseMcpPluginCreateModel createModel)
    {
        var url = "/api/mcp-plugins/sse";
        var response = await SendPostRequestMessage(url, createModel);
        
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException($"API endpoint not found: {url}");
        }
        
        if (response?.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new HttpRequestException($"{await response.Content.ReadAsStringAsync()}");
        }
        
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<McpPluginInfo>();
    }

    public async Task<McpPluginInfo> UpdateMcpPluginAsync(McpPluginUpdateModel updateModel)
    {
        var url = "/api/mcp-plugins/update";
        var response = await SendPutRequestMessage(url, updateModel);
        
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException($"Plugin not found with ID: {updateModel.Id}");
        }
        
        if (response?.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new HttpRequestException($"{await response.Content.ReadAsStringAsync()}");
        }
        
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<McpPluginInfo>();
    }

    public async Task DeleteMcpPluginAsync(Guid pluginId)
    {
        var url = $"/api/mcp-plugins/{pluginId}";
        var response = await SendDeleteRequestMessage(url);
        
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException($"Plugin not found with ID: {pluginId}");
        }
        
        response?.EnsureSuccessStatusCode();
    }

    public async Task<List<DocumentProcessInfo>> GetDocumentProcessesByPluginIdAsync(Guid pluginId)
    {
        try
        {
            var url = $"/api/mcp-plugins/{pluginId}/document-processes";
            var response = await SendGetRequestMessage(url);

            // If not found, just return an empty list rather than throwing
            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<DocumentProcessInfo>();
            }

            response?.EnsureSuccessStatusCode();
            return await response!.Content.ReadFromJsonAsync<List<DocumentProcessInfo>>() ?? new List<DocumentProcessInfo>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error getting document processes for plugin {pluginId}");
            return new List<DocumentProcessInfo>();
        }
    }

    public async Task<List<McpPluginAssociationInfo>> GetPluginAssociationsByPluginIdAsync(Guid pluginId)
    {
        var url = $"/api/mcp-plugins/{pluginId}/document-process-associations";
        var response = await SendGetRequestMessage(url);
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<McpPluginAssociationInfo>();
        }
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<List<McpPluginAssociationInfo>>() ?? new List<McpPluginAssociationInfo>();
    }

    public async Task<List<McpPluginAssociationInfo>> GetPluginAssociationsByDocumentProcessIdAsync(Guid documentProcessId)
    {
        // Use the correct endpoint that returns plugin associations with plugin name
        var url = $"/api/document-processes/{documentProcessId}/mcp-plugins";
        var response = await SendGetRequestMessage(url);
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<McpPluginAssociationInfo>();
        }
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<List<McpPluginAssociationInfo>>() ?? new List<McpPluginAssociationInfo>();
    }

    public async Task<string?> UpdateExposeToFlowAsync(Guid pluginId, bool exposeToFlow)
    {
        try
        {
            var url = $"/api/mcp-plugins/{pluginId}/expose-to-flow";
            var response = await SendPutRequestMessage(url, exposeToFlow);

            if (response?.StatusCode == HttpStatusCode.BadRequest)
            {
                // Return the error message from the API (e.g., list of templates using the plugin)
                return await response.Content.ReadAsStringAsync();
            }

            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return $"Plugin not found with ID: {pluginId}";
            }

            response?.EnsureSuccessStatusCode();
            return null; // Success
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error updating ExposeToFlow for plugin {pluginId}");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<List<McpPluginInfo>> GetFlowExposedPluginsAsync()
    {
        try
        {
            var url = "/api/mcp-plugins/flow-exposed";
            var response = await SendGetRequestMessage(url);

            if (response?.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<McpPluginInfo>();
            }

            response?.EnsureSuccessStatusCode();
            return await response!.Content.ReadFromJsonAsync<List<McpPluginInfo>>() ?? new List<McpPluginInfo>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting flow-exposed MCP plugins");
            return new List<McpPluginInfo>();
        }
    }
}
