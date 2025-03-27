namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Entry for the Plugin Registry - this stores an activated plugin instance and its key.
    /// </summary>
    public class PluginRegistryEntry
    {
        public string Key { get; set; }
        public object PluginInstance { get; set; }

        /// <summary>
        /// Indicates whether this plugin is dynamic.
        /// </summary>
        public bool IsDynamic { get; set; } = true;

    }
}