// Copyright (c) Microsoft Corporation. All rights reserved.
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts
{
    /// <summary>
    /// Grain interface for managing MCP plugins across the cluster.
    /// </summary>
    public interface IMcpPluginManagementGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Stops and removes a specific version of an MCP plugin from the container
        /// in the Silo where this grain is activated.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="versionString">The version string of the plugin to remove (e.g., "1.0.0").</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task StopAndRemovePluginVersionAsync(string pluginName, string versionString);
    }
}
