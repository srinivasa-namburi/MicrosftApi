namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Defines authentication types for MCP plugins.
    /// </summary>
    public enum McpPluginAuthenticationType
    {
        None = 0,
        GreenlightManagedIdentity = 1,
        /// <summary>
        /// Use a per-user bearer token stored in the UserTokenStore grain.
        /// </summary>
        UserBearerToken = 2
    }
}