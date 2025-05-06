using System;
using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.Plugins
{
    /// <summary>
    /// Represents an association between an MCP plugin and a document process.
    /// </summary>
    public class McpPluginDocumentProcess
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
        /// Gets or sets the document process definition identifier.
        /// </summary>
        public Guid DynamicDocumentProcessDefinitionId { get; set; }

        /// <summary>
        /// Gets or sets the specific version to use.
        /// If null, the latest version will be used.
        /// </summary>
        public McpPluginVersion? Version { get; set; }

        /// <summary>
        /// Gets or sets the specific version ID to use.
        /// </summary>
        public Guid? VersionId { get; set; }

        /// <summary>
        /// Gets or sets the MCP plugin.
        /// </summary>
        public McpPlugin? McpPlugin { get; set; }

        /// <summary>
        /// Gets or sets the document process definition.
        /// </summary>
        [JsonIgnore]
        public DynamicDocumentProcessDefinition? DynamicDocumentProcessDefinition { get; set; }

        /// <summary>
        /// Gets or sets whether this plugin is enabled for the document process.
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }
}