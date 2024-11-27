namespace Microsoft.Greenlight.Shared.Plugins
{
    public class DynamicPluginContainer
    {
        public Dictionary<string, Dictionary<string, LoadedDynamicPluginInfo>>? LoadedPlugins { get; set; }

        public void AddPlugin(string pluginName, string version, LoadedDynamicPluginInfo pluginInfo)
        {
            LoadedPlugins ??= new Dictionary<string, Dictionary<string, LoadedDynamicPluginInfo>>();
            
            if (!LoadedPlugins.ContainsKey(pluginName))
            {
                LoadedPlugins[pluginName] = new Dictionary<string, LoadedDynamicPluginInfo>();
            }
            LoadedPlugins[pluginName][version] = pluginInfo;
        }

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

        public IEnumerable<LoadedDynamicPluginInfo> GetAllPlugins()
        {
            return LoadedPlugins?.Values.SelectMany(v => v.Values) ?? Enumerable.Empty<LoadedDynamicPluginInfo>();
        }
    }
}