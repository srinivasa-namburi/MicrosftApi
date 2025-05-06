using System;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Represents information about an MCP plugin's association with a document process.
    /// </summary>
    public class McpPluginDocumentProcessInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the MCP plugin identifier.
        /// </summary>
        public Guid McpPluginId { get; set; }

        /// <summary>
        /// Gets or sets the document process identifier.
        /// </summary>
        public Guid DocumentProcessId { get; set; }

        /// <summary>
        /// Gets or sets the version identifier.
        /// </summary>
        public Guid? VersionId { get; set; }

        /// <summary>
        /// Gets or sets the plugin version.
        /// </summary>
        public McpPluginVersionInfo? Version { get; set; }

        /// <summary>
        /// Gets or sets whether this plugin is enabled for the document process.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the document process.
        /// </summary>
        public DocumentProcessInfo? DocumentProcess { get; set; }

        /// <summary>
        /// Gets or sets the plugin.
        /// </summary>
        public McpPluginInfo? Plugin { get; set; }
    }
}