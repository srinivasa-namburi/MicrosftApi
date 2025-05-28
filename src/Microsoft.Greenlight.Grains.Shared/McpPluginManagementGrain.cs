// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Streams;
using Microsoft.Greenlight.Shared.Plugins;
using Orleans;
using System;
using System.Threading.Tasks;

namespace Microsoft.Greenlight.Grains.Shared
{
    /// <summary>
    /// Grain implementation for managing MCP plugins across the cluster.
    /// </summary>
    public class McpPluginManagementGrain : Grain, IMcpPluginManagementGrain
    {
        private readonly McpPluginManager _pluginManager;
        private readonly ILogger<McpPluginManagementGrain> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPluginManagementGrain"/> class.
        /// </summary>
        /// <param name="pluginManager">The MCP plugin manager.</param>
        /// <param name="logger">The logger.</param>
        public McpPluginManagementGrain(McpPluginManager pluginManager, ILogger<McpPluginManagementGrain> logger)
        {
            _pluginManager = pluginManager;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task StopAndRemovePluginVersionAsync(string pluginName, string versionString)
        {
            _logger.LogInformation("McpPluginManagementGrain publishing plugin stop/remove for '{PluginName}' version '{Version}' to stream", pluginName, versionString);

            // Publish to Orleans Stream (all nodes will receive)
            var streamProvider = this.GetStreamProvider("StreamProvider");

            var stream = streamProvider.GetStream<PluginUpdate>(
                StreamId.Create(SystemStreamNameSpaces.PluginUpdateNamespace, Guid.Empty));
            
            var update = new PluginUpdate(pluginName, versionString, Guid.NewGuid());
            await stream.OnNextAsync(update);
        }
    }
}
