using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins
{
    /// <summary>
    /// Information about an MCP plugin version.
    /// </summary>
    public class McpPluginVersionInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the version.
        /// </summary>
        public Guid Id { get; set; }
        
        /// <summary>
        /// Gets or sets the major version number.
        /// </summary>
        public int Major { get; set; }

        /// <summary>
        /// Gets or sets the minor version number.
        /// </summary>
        public int Minor { get; set; }

        /// <summary>
        /// Gets or sets the patch version number.
        /// </summary>
        public int Patch { get; set; }

        /// <summary>
        /// Gets or sets the optional command to run for this version.
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Gets or sets the optional arguments for the command.
        /// </summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the environment variables for this plugin version.
        /// These will be applied to the process when the plugin runs.
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Returns a string representation of the plugin version.
        /// </summary>
        /// <returns>A string in the format "Major.Minor.Patch".</returns>
        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}