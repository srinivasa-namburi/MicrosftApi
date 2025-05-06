using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Represents information about an MCP plugin.
    /// </summary>
    public class McpPluginInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier of the plugin.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the plugin.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the plugin.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the source type of the plugin.
        /// </summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blob container name where the plugin is stored.
        /// </summary>
        public string? BlobContainerName { get; set; }

        /// <summary>
        /// Gets or sets the versions of the plugin.
        /// </summary>
        public List<McpPluginVersionInfo> Versions { get; set; } = new List<McpPluginVersionInfo>();

        /// <summary>
        /// Gets or sets the latest version of the plugin.
        /// </summary>
        public McpPluginVersionInfo? LatestVersion { get; set; }

        /// <summary>
        /// Gets or sets the document processes associated with this plugin.
        /// </summary>
        public List<McpPluginDocumentProcessInfo>? DocumentProcesses { get; set; } = new List<McpPluginDocumentProcessInfo>();
    }
}