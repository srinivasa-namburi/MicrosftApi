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
    Task AssociateMcpPluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, McpPluginVersionInfo version, bool keepOnLatestVersion = false);
    Task DisassociateMcpPluginFromDocumentProcessAsync(Guid pluginId, Guid documentProcessId);
    Task<(McpPluginInfo PluginInfo, bool NeedsOverride)> UploadMcpPluginAsyncWithOverrideCheck(IBrowserFile pluginFile);
    Task<McpPluginInfo> CreateCommandOnlyMcpPluginAsync(CommandOnlyMcpPluginCreateModel createModel);
    Task<McpPluginInfo> CreateSseMcpPluginAsync(SseMcpPluginCreateModel createModel);
    Task<McpPluginInfo> UpdateMcpPluginAsync(McpPluginUpdateModel updateModel);
    Task DeleteMcpPluginAsync(Guid pluginId);

    /// <summary>
    /// Gets plugin associations for a specific document process.
    /// </summary>
    /// <param name="documentProcessId">The document process ID.</param>
    /// <returns>List of plugin associations.</returns>
    Task<List<McpPluginAssociationInfo>> GetPluginAssociationsByDocumentProcessIdAsync(Guid documentProcessId);

    /// <summary>
    /// Gets document process associations for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>List of plugin associations.</returns>
    Task<List<McpPluginAssociationInfo>> GetPluginAssociationsByPluginIdAsync(Guid pluginId);

    /// <summary>
    /// Updates a plugin-document process association.
    /// </summary>
    /// <param name="documentProcessId">The document process ID.</param>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="update">The update data.</param>
    Task UpdateMcpPluginAssociationAsync(Guid documentProcessId, Guid pluginId, McpPluginAssociationInfo update);
}