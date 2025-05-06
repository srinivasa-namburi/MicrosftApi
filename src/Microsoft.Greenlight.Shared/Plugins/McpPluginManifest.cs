namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Represents an MCP plugin manifest.
    /// </summary>
    public class McpPluginManifest
    {
        /// <summary>
        /// Gets or sets the name of the MCP plugin.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the MCP plugin.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command to execute the MCP server.
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the arguments to pass to the MCP server command.
        /// </summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the environment variables to apply when running the command.
        /// Key is the environment variable name, value is the environment variable value.
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();
    }
}