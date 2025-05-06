using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Model for updating an MCP plugin of any source type.
    /// </summary>
    public class McpPluginUpdateModel
    {
        /// <summary>
        /// Gets or sets the unique identifier of the plugin.
        /// </summary>
        [Required(ErrorMessage = "Plugin ID is required")]
        public Guid Id { get; set; }
        
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
        /// Gets or sets the source type of the plugin.
        /// </summary>
        [Required(ErrorMessage = "Source type is required")]
        public string SourceType { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the version of the plugin.
        /// </summary>
        [Required(ErrorMessage = "Version is required")]
        public string Version { get; set; } = "1.0.0";
        
        /// <summary>
        /// Gets or sets the command to execute.
        /// </summary>
        public string? Command { get; set; }
        
        /// <summary>
        /// Gets or sets the arguments to pass to the command.
        /// </summary>
        public List<string>? Arguments { get; set; }

        /// <summary>
        /// Gets or sets the environment variables to apply when running the command.
        /// </summary>
        public Dictionary<string, string>? EnvironmentVariables { get; set; }
        
        /// <summary>
        /// Gets or sets the URL for SSE/HTTP plugins. Null for non-SSE plugins.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the authentication type for the plugin. Null for non-SSE plugins.
        /// </summary>
        public McpPluginAuthenticationType? AuthenticationType { get; set; }
    }
}