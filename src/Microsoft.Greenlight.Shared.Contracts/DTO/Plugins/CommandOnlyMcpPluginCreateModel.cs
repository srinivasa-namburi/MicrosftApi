using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Model for creating or updating a command-line only MCP plugin without a package file.
    /// </summary>
    public class CommandOnlyMcpPluginCreateModel
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
        /// Gets or sets the command to execute.
        /// </summary>
        [Required(ErrorMessage = "Command is required")]
        public string Command { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the arguments to pass to the command.
        /// </summary>
        public List<string>? Arguments { get; set; }

        /// <summary>
        /// Gets or sets the environment variables to apply when running the command.
        /// Key is the environment variable name, value is the environment variable value.
        /// </summary>
        public Dictionary<string, string>? EnvironmentVariables { get; set; }
    }
}