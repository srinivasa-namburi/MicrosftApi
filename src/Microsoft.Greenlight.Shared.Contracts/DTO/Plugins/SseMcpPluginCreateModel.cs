using System.ComponentModel.DataAnnotations;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Model for creating an SSE/HTTP MCP plugin.
    /// </summary>
    public class SseMcpPluginCreateModel
    {
        /// <summary>
        /// Gets or sets the name of the plugin.
        /// </summary>
        [Required(ErrorMessage = "Plugin name is required")]
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the description of the plugin.
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Gets or sets the version of the plugin.
        /// </summary>
        [Required(ErrorMessage = "Version is required")]
        public string Version { get; set; } = "1.0.0";
        
        /// <summary>
        /// Gets or sets the endpoint URL for the SSE plugin.
        /// </summary>
        [Required(ErrorMessage = "Endpoint URL is required")]
        [Url(ErrorMessage = "Please enter a valid URL")]
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the authentication type for the plugin.
        /// </summary>
        public McpPluginAuthenticationType AuthenticationType { get; set; } = McpPluginAuthenticationType.None;
    }
}