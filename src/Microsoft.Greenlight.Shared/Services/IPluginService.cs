using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Services;

public interface IPluginService
{
    Task AssociatePluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, string version);
    Task AssociatePluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, DynamicPluginVersion version);
    Task DisassociatePluginFromDocumentProcessAsync(Guid pluginId, Guid documentProcessId);
    Task<List<DynamicPlugin>> GetAllPluginsAsync();
    Task<DynamicPlugin?> GetPluginByIdAsync(Guid pluginId);
    Task<List<DynamicPlugin>> GetPluginsByDocumentProcessIdAsync(Guid documentProcessId);
}