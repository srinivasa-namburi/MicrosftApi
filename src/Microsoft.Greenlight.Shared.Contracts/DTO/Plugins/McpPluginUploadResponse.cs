using System;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Response model for MCP plugin upload operations.
    /// </summary>
    public class McpPluginUploadResponse
    {
        /// <summary>
        /// Gets or sets the plugin information.
        /// </summary>
        public McpPluginInfo PluginInfo { get; set; } = null!;
        
        /// <summary>
        /// Gets or sets a value indicating whether the plugin needs override information.
        /// </summary>
        public bool NeedsOverride { get; set; }
    }
}