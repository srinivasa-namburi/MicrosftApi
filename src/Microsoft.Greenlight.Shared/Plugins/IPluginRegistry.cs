using Microsoft.Greenlight.Extensions.Plugins;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Defines a Plugin Registry - instantiaed as a singleton in the DI container. Contains
    /// all plugins that have been registered.
    /// </summary>
    public interface IPluginRegistry
    {
        /// <summary>
        /// Add a Plugin to the  registry
        /// </summary>
        /// <param name="entry"></param>
        void AddPlugin(PluginRegistryEntry entry);
        /// <summary>
        /// Add a Plugin to the registry
        /// </summary>
        /// <param name="key"></param>
        /// <param name="plugin"></param>
        /// <param name="isDynamic"></param>
        void AddPlugin(string key, IPluginImplementation plugin, bool isDynamic = true);
        /// <summary>
        /// Read-only collection of all plugins currently present in the registry
        /// </summary>
        IReadOnlyCollection<PluginRegistryEntry> AllPlugins { get; }
    }
}