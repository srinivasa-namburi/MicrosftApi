namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Represents information about a dynamic plugin.
    /// </summary>
    public class DynamicPluginInfo
    {
        /// <summary>
        /// Unique identifier of the plugin.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name of the plugin.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Name of the blob container where the plugin is stored.
        /// </summary>
        public string BlobContainerName { get; set; }

        /// <summary>
        /// Latest version information of the plugin.
        /// </summary>
        public DynamicPluginVersionInfo LatestVersion { get; set; }

        /// <summary>
        /// List of all versions of the plugin.
        /// </summary>
        public List<DynamicPluginVersionInfo> Versions { get; set; }

        /// <summary>
        /// List of document processes associated with the plugin.
        /// </summary>
        public List<DynamicPluginDocumentProcessInfo> DocumentProcesses { get; set; }
    }
}
