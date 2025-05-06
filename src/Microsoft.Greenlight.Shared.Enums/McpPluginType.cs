namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Represents the type of MCP plugin.
    /// </summary>
    public enum McpPluginType
    {
        /// <summary>
        /// Standard input/output MCP plugin.
        /// </summary>
        Stdio = 0,

        /// <summary>
        /// HTTP-based MCP plugin.
        /// </summary>
        Http = 1,

        /// <summary>
        /// GitHub-hosted MCP plugin.
        /// </summary>
        GitHub = 2,

        /// <summary>
        /// NPX-executed MCP plugin.
        /// </summary>
        Npx = 3,

        /// <summary>
        /// SSE/HTTP MCP plugin.
        /// </summary>
        Sse = 4
    }
}