using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IPluginApiClient : IServiceClient
{
    Task<List<DynamicPluginInfo>> GetAllPluginsAsync();
    Task<DynamicPluginInfo> GetPluginByIdAsync(Guid pluginId);
    Task<List<DynamicPluginInfo>> GetPluginsByDocumentProcessIdAsync(Guid documentProcessId);
    Task AssociatePluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, DynamicPluginVersionInfo versionInfo);
    Task DisassociatePluginFromDocumentProcessAsync(Guid pluginId, Guid documentProcessId);
    Task<DynamicPluginInfo> UploadPluginAsync(IBrowserFile pluginFile);
 }