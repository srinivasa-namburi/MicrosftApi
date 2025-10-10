using Microsoft.Greenlight.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.Shared.Models.Plugins
{
    /// <summary>
    /// Represents an MCP plugin entity in the system.
    /// </summary>
    public class McpPlugin : EntityBase
    {
        /// <summary>
        /// Gets or sets the name of the plugin.
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the plugin.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the source type of the plugin.
        /// </summary>
        public McpPluginSourceType SourceType { get; set; }

        /// <summary>
        /// Gets or sets the blob container name for Azure Blob Storage plugins.
        /// </summary>
        public string? BlobContainerName { get; set; }

        /// <summary>
        /// Gets or sets the plugin versions.
        /// </summary>
        public ICollection<McpPluginVersion> Versions { get; set; } = new List<McpPluginVersion>();

        /// <summary>
        /// Gets or sets the document processes associated with this plugin.
        /// </summary>
        public ICollection<McpPluginDocumentProcess>? DocumentProcesses { get; set; } = new List<McpPluginDocumentProcess>();

        /// <summary>
        /// Gets or sets whether this plugin is exposed to Flow for use in Flow Tasks and conversational orchestration.
        /// </summary>
        public bool ExposeToFlow { get; set; }

        /// <summary>
        /// Gets the latest version of the plugin.
        /// </summary>
        public McpPluginVersion? LatestVersion
        {
            get
            {
                if (Versions == null || !Versions.Any())
                {
                    return null;
                }
                
                return Versions.OrderByDescending(v => v.Major)
                    .ThenByDescending(v => v.Minor)
                    .ThenByDescending(v => v.Patch)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets the blob name for the specified version.
        /// </summary>
        /// <param name="version">The plugin version.</param>
        /// <returns>The blob name.</returns>
        public string GetBlobName(McpPluginVersion version)
        {
            string versionString = $"{version.Major}.{version.Minor}.{version.Patch}";
            string fileName = $"{Name}_{versionString}.zip";
            return $"{Name}/{versionString}/{fileName}";
        }
    }
}
