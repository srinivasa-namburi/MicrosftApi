namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Defines the source type of an MCP plugin.
    /// </summary>
    public enum McpPluginSourceType
    {
        /// <summary>
        /// Plugin is stored in Azure Blob Storage.
        /// </summary>
        AzureBlobStorage = 0,
        
        /// <summary>
        /// Plugin is accessed via HTTP.
        /// </summary>
        Http = 1,
        
        /// <summary>
        /// Plugin is loaded from the local filesystem.
        /// </summary>
        LocalFilesystem = 2,
        
        /// <summary>
        /// Plugin is executed directly via command line without a package file.
        /// </summary>
        CommandOnly = 3
    }
}