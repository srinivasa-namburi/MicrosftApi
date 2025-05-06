using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Interface for Model Context Protocol (MCP) plugins.
    /// </summary>
    public interface IMcpPlugin
    {
        /// <summary>
        /// Gets the name of the MCP plugin.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the MCP plugin.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the version of the MCP plugin.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the type of the MCP plugin (stdio, http, etc.).
        /// </summary>
        McpPluginType Type { get; }
        
        /// <summary>
        /// Gets the MCP client used to communicate with the MCP server.
        /// </summary>
        IMcpClient? McpClient { get; }

        /// <summary>
        /// Gets the plugin manifest.
        /// </summary>
        McpPluginManifest? Manifest { get; }

        /// <summary>
        /// Initializes the MCP plugin asynchronously.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InitializeAsync(DocumentProcessInfo documentProcess);

        /// <summary>
        /// Gets the kernel functions for this MCP plugin.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>A task containing the list of kernel functions.</returns>
        Task<IList<KernelFunction>> GetKernelFunctionsAsync(DocumentProcessInfo documentProcess);
        
        /// <summary>
        /// Stops the MCP plugin.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StopAsync();

        /// <summary>
        /// Disposes of the MCP plugin resources.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task DisposeAsync();
    }
}
