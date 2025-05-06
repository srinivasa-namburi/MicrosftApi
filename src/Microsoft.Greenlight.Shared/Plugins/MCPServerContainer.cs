using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Container for MCP server instances.
    /// </summary>
    public class MCPServerContainer
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IMcpPlugin>> _mcpPlugins = new();
        private readonly ILogger<MCPServerContainer>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MCPServerContainer"/> class.
        /// </summary>
        /// <param name="logger">Optional logger.</param>
        public MCPServerContainer(ILogger<MCPServerContainer>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Adds an MCP plugin to the container.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="version">The version of the plugin.</param>
        /// <param name="plugin">The MCP plugin instance.</param>
        public void AddPlugin(string pluginName, string version, IMcpPlugin plugin)
        {
            var versionDictionary = _mcpPlugins.GetOrAdd(pluginName, _ => new ConcurrentDictionary<string, IMcpPlugin>());
            versionDictionary.AddOrUpdate(version, plugin, (_, _) => plugin);
            
            _logger?.LogInformation("Added MCP plugin to container: {PluginName}, version: {Version}", pluginName, version);
        }

        /// <summary>
        /// Tries to get an MCP plugin from the container.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="version">The version of the plugin.</param>
        /// <param name="plugin">When this method returns, contains the MCP plugin associated with the specified name and version, if found; otherwise, null.</param>
        /// <returns>true if the plugin was found; otherwise, false.</returns>
        public bool TryGetPlugin(string pluginName, string version, out IMcpPlugin? plugin)
        {
            plugin = null;
            
            if (_mcpPlugins.TryGetValue(pluginName, out var versionDictionary) &&
                versionDictionary.TryGetValue(version, out var foundPlugin))
            {
                plugin = foundPlugin;
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Gets all MCP plugins for a specific plugin name.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <returns>All versions of the specified plugin.</returns>
        public IEnumerable<IMcpPlugin> GetPluginVersions(string pluginName)
        {
            if (_mcpPlugins.TryGetValue(pluginName, out var versionDictionary))
            {
                return versionDictionary.Values;
            }
            
            return Enumerable.Empty<IMcpPlugin>();
        }

        /// <summary>
        /// Gets all MCP plugins in the container.
        /// </summary>
        /// <returns>All MCP plugins in the container.</returns>
        public IEnumerable<IMcpPlugin> GetAllPlugins()
        {
            foreach (var versionDictionary in _mcpPlugins.Values)
            {
                foreach (var plugin in versionDictionary.Values)
                {
                    yield return plugin;
                }
            }
        }

        /// <summary>
        /// Removes an MCP plugin from the container.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="version">The version of the plugin.</param>
        /// <returns>true if the plugin was successfully removed; otherwise, false.</returns>
        public bool RemovePlugin(string pluginName, string version)
        {
            if (_mcpPlugins.TryGetValue(pluginName, out var versionDictionary))
            {
                if (versionDictionary.TryRemove(version, out var removedPlugin))
                {
                    // If this was the last version, remove the plugin entry altogether
                    if (versionDictionary.IsEmpty)
                    {
                        _mcpPlugins.TryRemove(pluginName, out _);
                    }
                    
                    // Dispose the removed plugin
                    _ = removedPlugin.DisposeAsync();
                    
                    _logger?.LogInformation("Removed MCP plugin from container: {PluginName}, version: {Version}", pluginName, version);
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Disposes all MCP plugins in the container.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task DisposeAllPluginsAsync()
        {
            _logger?.LogInformation("Disposing all MCP plugins in container");
            
            var disposeTasks = GetAllPlugins().Select(plugin => plugin.DisposeAsync()).ToArray();
            await Task.WhenAll(disposeTasks);
            
            _mcpPlugins.Clear();
        }
    }
}