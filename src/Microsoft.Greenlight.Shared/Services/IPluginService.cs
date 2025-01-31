using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// An interface for a service to manage plugins and their associations with document processes.
/// </summary>
public interface IPluginService
{
    /// <summary>
    /// Associates a plugin with a document process using a string version.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin.</param>
    /// <param name="documentProcessId">The ID of the document process.</param>
    /// <param name="version">The version of the plugin.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task AssociatePluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, string version);

    /// <summary>
    /// Associates a plugin with a document process using <see cref="DynamicPluginVersion"/>.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin.</param>
    /// <param name="documentProcessId">The ID of the document process.</param>
    /// <param name="version">The version of the plugin.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task AssociatePluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, DynamicPluginVersion version);

    /// <summary>
    /// Disassociates a plugin from a document process.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin.</param>
    /// <param name="documentProcessId">The ID of the document process.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task DisassociatePluginFromDocumentProcessAsync(Guid pluginId, Guid documentProcessId);

    /// <summary>
    /// Gets all plugins.
    /// </summary>
    /// <returns>A list of all plugins.</returns>
    Task<List<DynamicPlugin>> GetAllPluginsAsync();

    /// <summary>
    /// Gets a plugin by its ID.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin.</param>
    /// <returns>The plugin if found; otherwise, null.</returns>
    Task<DynamicPlugin?> GetPluginByIdAsync(Guid pluginId);

    /// <summary>
    /// Gets plugins associated with a specific document process.
    /// </summary>
    /// <param name="documentProcessId">The ID of the document process.</param>
    /// <returns>A list of plugins associated with the document process.</returns>
    Task<List<DynamicPlugin>> GetPluginsByDocumentProcessIdAsync(Guid documentProcessId);
}
