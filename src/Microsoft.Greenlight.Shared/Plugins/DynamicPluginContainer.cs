namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Container for managing dynamic plugins.
    /// </summary>
    public class DynamicPluginContainer
    {
        /// <summary>
        /// Gets or sets the loaded plugins.
        /// </summary>
        public Dictionary<string, Dictionary<string, LoadedDynamicPluginInfo>>? LoadedPlugins { get; set; }

        /// <summary>
        /// Adds a plugin to the container.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="version">The version of the plugin.</param>
        /// <param name="pluginInfo">The plugin information.</param>
        public void AddPlugin(string pluginName, string version, LoadedDynamicPluginInfo pluginInfo)
        {
            LoadedPlugins ??= new Dictionary<string, Dictionary<string, LoadedDynamicPluginInfo>>();

            if (!LoadedPlugins.TryGetValue(pluginName, out Dictionary<string, LoadedDynamicPluginInfo>? value))
            {
                value = new Dictionary<string, LoadedDynamicPluginInfo>();
                LoadedPlugins[pluginName] = value;
            }

            value[version] = pluginInfo;
        }

        /// <summary>
        /// Tries to get a plugin from the container.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="version">The version of the plugin.</param>
        /// <param name="pluginInfo">The plugin information.</param>
        /// <returns>True if the plugin is found; otherwise, false.</returns>
        public bool TryGetPlugin(string pluginName, string version, out LoadedDynamicPluginInfo? pluginInfo)
        {
            pluginInfo = null;
            if (LoadedPlugins == null)
            {
                return false;
            }

            if (LoadedPlugins.TryGetValue(pluginName, out var versionDict))
            {
                return versionDict.TryGetValue(version, out pluginInfo);
            }
            return false;
        }

        /// <summary>
        /// Gets all plugins from the container.
        /// </summary>
        /// <returns>An enumerable collection of all loaded plugins.</returns>
        public IEnumerable<LoadedDynamicPluginInfo> GetAllPlugins()
        {
            return LoadedPlugins?.Values.SelectMany(v => v.Values) ?? Enumerable.Empty<LoadedDynamicPluginInfo>();
        }
    }
}