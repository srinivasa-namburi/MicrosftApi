using Microsoft.Greenlight.Extensions.Plugins;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <inheritdoc />
    public class DefaultPluginRegistry : IPluginRegistry
    {
        private readonly List<PluginRegistryEntry> _allPlugins = [];

        /// <inheritdoc />
        public void AddPlugin(PluginRegistryEntry entry)
        {
            if (_allPlugins.Any(x => x.Key == entry.Key))
            {
                // Replace the existing plugin with the new one.
                _allPlugins.Remove(_allPlugins.First(x => x.Key == entry.Key));
            }

            _allPlugins.Add(entry);
        }

        /// <inheritdoc />
        public void AddPlugin(string key, IPluginImplementation plugin, bool isDynamic = true)
        {
            var pluginRegistryEntry = new PluginRegistryEntry()
            {
                Key = key, PluginInstance = plugin, IsDynamic = isDynamic
            };

            AddPlugin(pluginRegistryEntry);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<PluginRegistryEntry> AllPlugins => _allPlugins;
    }
}