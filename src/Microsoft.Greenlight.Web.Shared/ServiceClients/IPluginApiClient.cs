using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IPluginApiClient : IServiceClient
{
    Task<List<McpPluginInfo>> GetAllMcpPluginsAsync();
    Task<McpPluginInfo> GetMcpPluginByIdAsync(Guid pluginId);
    Task<List<McpPluginInfo>> GetMcpPluginsByDocumentProcessIdAsync(Guid documentProcessId);
    Task<List<DocumentProcessInfo>> GetDocumentProcessesByPluginIdAsync(Guid pluginId);
    Task AssociateMcpPluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, McpPluginVersionInfo version);
    Task DisassociateMcpPluginFromDocumentProcessAsync(Guid pluginId, Guid documentProcessId);
    Task<(McpPluginInfo PluginInfo, bool NeedsOverride)> UploadMcpPluginAsyncWithOverrideCheck(IBrowserFile pluginFile);
    Task UpdateMcpPluginVersionAsync(Guid documentProcessId, Guid pluginId, McpPluginVersionInfo version);
    Task<McpPluginInfo> CreateCommandOnlyMcpPluginAsync(CommandOnlyMcpPluginCreateModel createModel);
    Task<McpPluginInfo> UpdateMcpPluginAsync(McpPluginUpdateModel updateModel);
    Task DeleteMcpPluginAsync(Guid pluginId);
}