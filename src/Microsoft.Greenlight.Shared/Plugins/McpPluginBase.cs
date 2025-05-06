using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Base class for MCP plugin implementations.
    /// </summary>
    public abstract class McpPluginBase : IMcpPlugin
    {
        /// <summary>
        /// Gets the name of the MCP plugin.
        /// </summary>
        public string Name { get; protected set; } = string.Empty;

        /// <summary>
        /// Gets the description of the MCP plugin.
        /// </summary>
        public string Description { get; protected set; } = string.Empty;

        /// <summary>
        /// Gets the version of the MCP plugin.
        /// </summary>
        public string Version { get; protected set; } = string.Empty;

        /// <summary>
        /// Gets the type of the MCP plugin.
        /// </summary>
        public McpPluginType Type { get; protected set; } = McpPluginType.Stdio;

        /// <summary>
        /// Gets the MCP client used to communicate with the MCP server.
        /// </summary>
        public virtual IMcpClient? McpClient { get; protected set; }

        /// <summary>
        /// Gets the plugin manifest.
        /// </summary>
        public McpPluginManifest? Manifest { get; protected set; }

        /// <summary>
        /// Initializes the MCP plugin asynchronously.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task InitializeAsync(DocumentProcessInfo documentProcess);

        /// <summary>
        /// Gets the kernel functions for this MCP plugin.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>A task containing the list of kernel functions.</returns>
        public abstract Task<IList<KernelFunction>> GetKernelFunctionsAsync(DocumentProcessInfo documentProcess);

        /// <summary>
        /// Starts the MCP plugin.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task StartAsync();

        /// <summary>
        /// Stops the MCP plugin.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task StopAsync();

        /// <summary>
        /// Disposes of the MCP plugin resources.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public abstract Task DisposeAsync();
    }
}