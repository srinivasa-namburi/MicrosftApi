using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Manages the lifecycle and operations of MCP plugins.
    /// </summary>
    public class McpPluginManager
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly MCPServerContainer _pluginContainer;
        private readonly ILogger<McpPluginManager>? _logger;
        private bool _pluginsLoaded;
        private readonly object _lockObject = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPluginManager"/> class.
        /// </summary>
        /// <param name="serviceScopeFactory">The service scope factory.</param>
        /// <param name="pluginContainer">The MCP plugin container.</param>
        /// <param name="logger">Optional logger.</param>
        public McpPluginManager(
            IServiceScopeFactory serviceScopeFactory,
            MCPServerContainer pluginContainer,
            ILogger<McpPluginManager>? logger = null)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _pluginContainer = pluginContainer;
            _logger = logger;
        }

        /// <summary>
        /// Gets the MCP plugin container.
        /// </summary>
        public MCPServerContainer PluginContainer => _pluginContainer;

        /// <summary>
        /// Ensures that plugins are pre-loaded for better performance.
        /// Note: This is an optimization and the system will still work if this fails,
        /// as plugins are loaded on-demand when requested if not already loaded.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task EnsurePluginsLoadedAsync()
        {
            if (!_pluginsLoaded)
            {
                var loadLock = new SemaphoreSlim(1, 1);
                await loadLock.WaitAsync();
                
                try
                {
                    if (!_pluginsLoaded) // Double-check locking pattern
                    {
                        try
                        {
                            await LoadMcpPluginsAsync();
                            _pluginsLoaded = true;
                            _logger?.LogInformation("Pre-loaded all MCP plugins successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error pre-loading MCP plugins. Plugins will be loaded on demand instead.");
                        }
                    }
                }
                finally
                {
                    loadLock.Release();
                }
            }
        }

        /// <summary>
        /// Gets the plugin information for a specific document process asynchronously.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <param name="pluginName">The plugin name.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the MCP plugin, if found; otherwise, null.</returns>
        public async Task<IMcpPlugin?> GetPluginForDocumentProcessAsync(DocumentProcessInfo documentProcess, string pluginName)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<DocGenerationDbContext>();

            if (dbContext == null)
            {
                _logger?.LogWarning("Unable to get plugin '{PluginName}' for document process '{ProcessName}': DbContext not available",
                    pluginName, documentProcess.ShortName);
                return null;
            }

            try
            {
                var pluginAssociation = await dbContext.McpPluginDocumentProcesses
                    .Include(p => p.McpPlugin)
                    .Include(p => p.Version)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p =>
                        p.DynamicDocumentProcessDefinitionId == documentProcess.Id &&
                        p.McpPlugin != null &&
                        p.McpPlugin.Name == pluginName);

                if (pluginAssociation?.McpPlugin == null)
                {
                    return null;
                }

                var plugin = pluginAssociation.McpPlugin;
                var versionToUse = pluginAssociation.Version ?? plugin.LatestVersion;

                if (versionToUse == null)
                {
                    _logger?.LogWarning("No version available for plugin '{PluginName}'", pluginName);
                    return null;
                }

                // Try to get the plugin from the container
                if (_pluginContainer.TryGetPlugin(plugin.Name, versionToUse.ToString()!, out var mcpPlugin) && mcpPlugin != null)
                {
                    return mcpPlugin;
                }
                
                // If not in container, load it dynamically
                _logger?.LogInformation("Plugin '{PluginName}' version '{Version}' not found in container, loading dynamically", 
                    plugin.Name, versionToUse);
                
                // Load the plugin based on its source type
                mcpPlugin = await LoadPluginDynamicallyAsync(plugin, versionToUse);
                return mcpPlugin;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting plugin '{PluginName}' for document process '{ProcessName}'",
                    pluginName, documentProcess.ShortName);
                return null;
            }
        }

        /// <summary>
        /// Gets all plugins for a specific document process asynchronously.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the MCP plugins.</returns>
        public async Task<IEnumerable<IMcpPlugin>> GetPluginsForDocumentProcessAsync(DocumentProcessInfo documentProcess)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<DocGenerationDbContext>();

            if (dbContext == null)
            {
                _logger?.LogWarning("Unable to get plugins for document process '{ProcessName}': DbContext not available",
                    documentProcess.ShortName);
                return Enumerable.Empty<IMcpPlugin>();
            }

            try
            {
                var result = new List<IMcpPlugin>();

                var pluginAssociations = await dbContext.McpPluginDocumentProcesses
                    .Include(p => p.McpPlugin)
                        .ThenInclude(v=>v!.Versions)
                    .Include(p => p.Version)
                    .AsNoTracking()
                    .Where(p => p.DynamicDocumentProcessDefinitionId == documentProcess.Id && p.McpPlugin != null)
                    .ToListAsync();

                foreach (var association in pluginAssociations)
                {
                    if (association.McpPlugin == null)
                    {
                        continue;
                    }

                    var plugin = association.McpPlugin;
                    McpPluginVersion? versionToUse = null;
                    if (association.KeepOnLatestVersion)
                    {
                        versionToUse = plugin.LatestVersion;
                    }
                    else
                    {
                        versionToUse = association.Version ?? plugin.LatestVersion;
                    }

                    if (versionToUse == null)
                    {
                        _logger?.LogWarning("No version available for plugin '{PluginName}'", plugin.Name);
                        continue;
                    }

                    // Try to get the plugin from the container
                    if (_pluginContainer.TryGetPlugin(plugin.Name, versionToUse.ToString()!, out var mcpPlugin) && mcpPlugin != null)
                    {
                        result.Add(mcpPlugin);
                        continue;
                    }
                    
                    // If not in container, load it dynamically
                    _logger?.LogInformation("Plugin '{PluginName}' version '{Version}' not found in container, loading dynamically", 
                        plugin.Name, versionToUse);
                    
                    // Load the plugin based on its source type
                    mcpPlugin = await LoadPluginDynamicallyAsync(plugin, versionToUse);
                    if (mcpPlugin != null)
                    {
                        result.Add(mcpPlugin);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting plugins for document process '{ProcessName}'",
                    documentProcess.ShortName);
                return Enumerable.Empty<IMcpPlugin>();
            }
        }

        /// <summary>
        /// Loads a specific plugin from the database into the container.
        /// </summary>
        /// <param name="pluginId">The ID of the plugin to load.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task LoadPluginByIdAsync(Guid pluginId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<DocGenerationDbContext>();
            var azureFileHelper = scope.ServiceProvider.GetService<AzureFileHelper>();
            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();

            if (dbContext == null)
            {
                _logger?.LogWarning("Unable to load MCP plugin: required services not available");
                return;
            }

            try
            {
                var plugin = await dbContext.McpPlugins
                    .Include(p => p.Versions)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == pluginId);

                if (plugin == null)
                {
                    _logger?.LogWarning("Plugin with ID {PluginId} not found", pluginId);
                    return;
                }

                foreach (var version in plugin.Versions)
                {
                    try
                    {
                        switch (plugin.SourceType)
                        {
                            case McpPluginSourceType.AzureBlobStorage:
                                if (azureFileHelper != null)
                                {
                                    await LoadMcpPluginFromBlobStorageAsync(plugin, version, azureFileHelper, loggerFactory);
                                }
                                else
                                {
                                    _logger?.LogWarning("Skipping AzureBlobStorage plugin '{PluginName}' version '{Version}': AzureFileHelper not available",
                                        plugin.Name, version);
                                }
                                break;

                            case McpPluginSourceType.CommandOnly:
                                LoadCommandOnlyMcpPlugin(plugin, version, loggerFactory);
                                break;

                            case McpPluginSourceType.SSE:
                                LoadSseMcpPlugin(plugin, version, loggerFactory);
                                break;

                            default:
                                _logger?.LogWarning("Skipping plugin '{PluginName}' with unsupported source type: {SourceType}",
                                    plugin.Name, plugin.SourceType);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error loading MCP plugin '{PluginName}' version {Version}",
                            plugin.Name, version);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading plugin with ID {PluginId}", pluginId);
            }
        }

        /// <summary>
        /// Loads all MCP plugins from the database into the plugin container.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task LoadMcpPluginsAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<DocGenerationDbContext>();
            var azureFileHelper = scope.ServiceProvider.GetService<AzureFileHelper>();
            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();

            if (dbContext == null)
            {
                _logger?.LogWarning("Unable to load MCP plugins: required services not available");
                return;
            }

            List<McpPlugin> mcpPlugins;
            try
            {
                mcpPlugins = await dbContext.McpPlugins
                    .Include(p => p.Versions)
                    .AsNoTracking()
                    .ToListAsync();

                _logger?.LogInformation("Found {Count} MCP plugins to load", mcpPlugins.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unable to load MCP plugins");
                return;
            }

            foreach (var plugin in mcpPlugins)
            {
                foreach (var version in plugin.Versions)
                {
                    try
                    {
                        switch (plugin.SourceType)
                        {
                            case McpPluginSourceType.AzureBlobStorage:
                                if (azureFileHelper != null)
                                {
                                    await LoadMcpPluginFromBlobStorageAsync(plugin, version, azureFileHelper, loggerFactory);
                                }
                                else
                                {
                                    _logger?.LogWarning("Skipping AzureBlobStorage plugin '{PluginName}' version '{Version}': AzureFileHelper not available",
                                        plugin.Name, version);
                                }
                                break;

                            case McpPluginSourceType.CommandOnly:
                                LoadCommandOnlyMcpPlugin(plugin, version, loggerFactory);
                                break;

                            case McpPluginSourceType.SSE:
                                LoadSseMcpPlugin(plugin, version, loggerFactory);
                                break;

                            default:
                                _logger?.LogWarning("Skipping plugin '{PluginName}' with unsupported source type: {SourceType}",
                                    plugin.Name, plugin.SourceType);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error loading MCP plugin '{PluginName}' version {Version}",
                            plugin.Name, version);
                    }
                }
            }
        }

        /// <summary>
        /// Dynamically loads a plugin into the container if it's not already loaded.
        /// </summary>
        /// <param name="plugin">The plugin to load.</param>
        /// <param name="version">The version of the plugin to load.</param>
        /// <returns>The loaded plugin instance, or null if loading failed.</returns>
        private async Task<IMcpPlugin?> LoadPluginDynamicallyAsync(McpPlugin plugin, McpPluginVersion version)
        {
            try
            {
                // First check if it's already in the container (double check in case it was loaded in parallel)
                if (_pluginContainer.TryGetPlugin(plugin.Name, version.ToString()!, out var existingPlugin) && existingPlugin != null)
                {
                    return existingPlugin;
                }

                // Get services needed for loading
                using var scope = _serviceScopeFactory.CreateScope();
                var azureFileHelper = scope.ServiceProvider.GetService<AzureFileHelper>();
                var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();

                // Load based on source type
                switch (plugin.SourceType)
                {
                    case McpPluginSourceType.AzureBlobStorage:
                        if (azureFileHelper != null)
                        {
                            await LoadMcpPluginFromBlobStorageAsync(plugin, version, azureFileHelper, loggerFactory);
                        }
                        else
                        {
                            _logger?.LogWarning("Cannot load AzureBlobStorage plugin '{PluginName}' version '{Version}': AzureFileHelper not available",
                                plugin.Name, version);
                            return null;
                        }
                        break;

                    case McpPluginSourceType.CommandOnly:
                        LoadCommandOnlyMcpPlugin(plugin, version, loggerFactory);
                        break;

                    case McpPluginSourceType.SSE:
                        LoadSseMcpPlugin(plugin, version, loggerFactory);
                        break;

                    default:
                        _logger?.LogWarning("Cannot load plugin '{PluginName}' with unsupported source type: {SourceType}",
                            plugin.Name, plugin.SourceType);
                        return null;
                }

                // Check if loading was successful
                if (_pluginContainer.TryGetPlugin(plugin.Name, version.ToString()!, out var loadedPlugin))
                {
                    _logger?.LogInformation("Successfully loaded plugin '{PluginName}' version '{Version}' on demand",
                        plugin.Name, version);
                    return loadedPlugin;
                }

                _logger?.LogWarning("Failed to load plugin '{PluginName}' version '{Version}' on demand",
                    plugin.Name, version);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error dynamically loading plugin '{PluginName}' version '{Version}'",
                    plugin.Name, version);
                return null;
            }
        }

        private async Task<IMcpPlugin?> LoadMcpPluginFromBlobStorageAsync(
            McpPlugin plugin,
            McpPluginVersion version,
            AzureFileHelper azureFileHelper,
            ILoggerFactory? loggerFactory)
        {
            var pluginStream = await azureFileHelper.GetFileAsStreamFromContainerAndBlobName(
                plugin.BlobContainerName, plugin.GetBlobName(version));

            if (pluginStream == null)
            {
                _logger?.LogWarning("Failed to download plugin: {PluginName}, version: {Version}",
                    plugin.Name, version);
                return null;
            }

            var pluginDirectory = GetPluginWorkingDirectory(plugin, version);
            Directory.CreateDirectory(pluginDirectory);

            try
            {
                // Clean the directory
                foreach (var file in Directory.GetFiles(pluginDirectory, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException ex)
                    {
                        _logger?.LogWarning(ex, "Error deleting file {File}", file);
                    }
                }

                // Extract the plugin
                using (var archive = new ZipArchive(pluginStream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    archive.ExtractToDirectory(pluginDirectory);
                }

                McpPluginManifest? manifest = new();
                // Look for the manifest file
                var manifestPath = Path.Combine(pluginDirectory, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    _logger?.LogInformation(
                        "Manifest file not found for plugin: {PluginName}, version: {Version} - using stored command/arguments",
                        plugin.Name, version);
                }
                else
                {

                    // Read the manifest
                    var manifestJson = await File.ReadAllTextAsync(manifestPath);
                    manifest = JsonSerializer.Deserialize<McpPluginManifest>(manifestJson);
                }

                if (manifest == null)
                {
                    _logger?.LogWarning("Invalid manifest for plugin: {PluginName}, version: {Version}",
                        plugin.Name, version);
                    return null;
                }

                // Override manifest name and description with the ones from the database if necessary
                if (string.IsNullOrWhiteSpace(manifest.Name))
                {
                    manifest.Name = plugin.Name;
                }

                if (string.IsNullOrWhiteSpace(manifest.Description) && !string.IsNullOrWhiteSpace(plugin.Description))
                {
                    manifest.Description = plugin.Description;
                }

                // Override the command and arguments if they are specified in the version
                if (!string.IsNullOrWhiteSpace(version.Command))
                {
                    manifest.Command = version.Command;
                }

                if (version.Arguments.Any())
                {
                    manifest.Arguments = version.Arguments;
                }

                if (manifest.Name == string.Empty || manifest.Command == string.Empty)
                {
                    _logger?.LogWarning("Unable to construct manifest for plugin: {PluginName}, version: {Version}",
                        plugin.Name, version);
                    return null;
                }

                // Create the plugin instance
                var logger = loggerFactory?.CreateLogger<StdioMcpPlugin>();
                var mcpPlugin = new StdioMcpPlugin(manifest, pluginDirectory, version.ToString(), logger);

                // Add the plugin to the container
                _pluginContainer.AddPlugin(plugin.Name, version.ToString(), mcpPlugin);

                _logger?.LogInformation("Successfully loaded MCP plugin: {PluginName}, version: {Version}",
                    plugin.Name, version);

                return mcpPlugin;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading MCP plugin {PluginName}, version {Version}",
                    plugin.Name, version);
                return null;
            }
            finally
            {
                await pluginStream.DisposeAsync();
            }
        }

        private IMcpPlugin? LoadCommandOnlyMcpPlugin(
            McpPlugin plugin,
            McpPluginVersion version,
            ILoggerFactory? loggerFactory)
        {
            if (string.IsNullOrWhiteSpace(version.Command))
            {
                _logger?.LogWarning("Command is not specified for CommandOnly plugin: {PluginName}, version: {Version}",
                    plugin.Name, version);
                return null;
            }

            // Create a temporary working directory for this plugin instance
            var workingDirectory = GetPluginWorkingDirectory(plugin, version);
            Directory.CreateDirectory(workingDirectory);

            try
            {
                // Create a manifest for the command-only plugin
                var manifest = new McpPluginManifest
                {
                    Name = plugin.Name,
                    Description = plugin.Description ?? $"{plugin.Name} plugin",
                    Command = version.Command,
                    Arguments = version.Arguments
                };

                // Create the plugin instance
                var logger = loggerFactory?.CreateLogger<StdioMcpPlugin>();
                var mcpPlugin = new StdioMcpPlugin(manifest, workingDirectory, version.ToString(), logger);

                // Add the plugin to the container
                _pluginContainer.AddPlugin(plugin.Name, version.ToString(), mcpPlugin);

                _logger?.LogInformation("Successfully loaded CommandOnly MCP plugin: {PluginName}, version: {Version}",
                    plugin.Name, version);

                return mcpPlugin;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading CommandOnly MCP plugin {PluginName}, version {Version}",
                    plugin.Name, version);
                return null;
            }
        }

        private IMcpPlugin? LoadSseMcpPlugin(
            McpPlugin plugin,
            McpPluginVersion version,
            ILoggerFactory? loggerFactory)
        {
            try
            {
                // Create a temporary working directory for this plugin instance
                var workingDirectory = GetPluginWorkingDirectory(plugin, version);
                Directory.CreateDirectory(workingDirectory);

                // Create a manifest for the SSE plugin
                var manifest = new McpPluginManifest
                {
                    Name = plugin.Name,
                    Description = plugin.Description ?? $"{plugin.Name} plugin",
                    Url = version.Url,
                    AuthenticationType = version.AuthenticationType
                };

                // Create the plugin instance
                var logger = loggerFactory?.CreateLogger<SseMcpPlugin>();
                var mcpPlugin = new SseMcpPlugin(manifest, version.ToString(), logger);

                // Add the plugin to the container
                _pluginContainer.AddPlugin(plugin.Name, version.ToString(), mcpPlugin);

                _logger?.LogInformation("Successfully loaded SSE MCP plugin: {PluginName}, version: {Version}",
                    plugin.Name, version);

                return mcpPlugin;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading SSE MCP plugin {PluginName}, version {Version}",
                    plugin.Name, version);
                return null;
            }
        }

        /// <summary>
        /// Stops and removes a specific version of an MCP plugin from the container.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="versionString">The version string of the plugin to remove (e.g., "1.0.0").</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task StopAndRemovePluginVersionAsync(string pluginName, string versionString)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
            {
                _logger?.LogWarning("Cannot remove plugin version: Plugin name is null or empty.");
                return;
            }
            if (string.IsNullOrWhiteSpace(versionString))
            {
                _logger?.LogWarning("Cannot remove plugin version for '{PluginName}': Version string is null or empty.", pluginName);
                return;
            }

            if (_pluginContainer.TryGetPlugin(pluginName, versionString, out var pluginInstance) && pluginInstance != null)
            {
                _logger?.LogInformation("Stopping plugin '{PluginName}' version '{Version}' before removal.", pluginName, versionString);
                try
                {
                    await pluginInstance.StopAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error stopping plugin '{PluginName}' version '{Version}' during removal.", pluginName, versionString);
                    // Continue with removal even if stopping fails
                }
            }

            if (_pluginContainer.RemovePlugin(pluginName, versionString))
            {
                _logger?.LogInformation("Successfully removed plugin '{PluginName}' version '{Version}' from container.", pluginName, versionString);
            }
            else
            {
                _logger?.LogWarning("Plugin '{PluginName}' version '{Version}' not found in container for removal, or already removed.", pluginName, versionString);
            }
        }

        /// <summary>
        /// Processes the uploaded plugin file, checks for a manifest.json, and allows overrides if missing.
        /// </summary>
        /// <param name="pluginStream">The plugin file stream.</param>
        /// <param name="plugin">The plugin entity.</param>
        /// <param name="version">The plugin version entity.</param>
        /// <returns>A flag indicating whether overrides are needed.</returns>
        public async Task<bool> ProcessUploadedPluginAsync(Stream pluginStream, McpPlugin plugin, McpPluginVersion version)
        {
            var pluginDirectory = GetPluginWorkingDirectory(plugin, version);
            Directory.CreateDirectory(pluginDirectory);

            try
            {
                // Clean the directory
                foreach (var file in Directory.GetFiles(pluginDirectory, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException ex)
                    {
                        _logger?.LogWarning(ex, "Error deleting file {File}", file);
                    }
                }

                // Extract the plugin
                using (var archive = new ZipArchive(pluginStream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    archive.ExtractToDirectory(pluginDirectory);
                }

                // Look for the manifest file
                var manifestPath = Path.Combine(pluginDirectory, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    _logger?.LogWarning("Manifest file not found for plugin: {PluginName}, version: {Version}",
                        plugin.Name, version);
                    return true; // Overrides needed
                }

                // Read the manifest
                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<McpPluginManifest>(manifestJson);

                if (manifest == null)
                {
                    _logger?.LogWarning("Invalid manifest for plugin: {PluginName}, version: {Version}",
                        plugin.Name, version);
                    return true; // Overrides needed
                }

                // Always set plugin and version details from manifest
                plugin.Description = manifest.Description;
                version.Command = manifest.Command;
                version.Arguments = manifest.Arguments;
                version.EnvironmentVariables = manifest.EnvironmentVariables;

                return false; // No overrides needed
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing uploaded plugin {PluginName}, version {Version}",
                    plugin.Name, version);
                throw;
            }
            finally
            {
                await pluginStream.DisposeAsync();
            }
        }

        private static string GetPluginWorkingDirectory(McpPlugin plugin, McpPluginVersion version)
        {
            var directoryElements = new List<string>
            {
                "greenlight-mcp-plugins",
                Environment.MachineName,
                AppDomain.CurrentDomain.FriendlyName,
                "process-" + Environment.ProcessId.ToString(),
                plugin.Name,
                version.ToString()
            };

            var pluginDirectory = Path.Combine(Path.GetTempPath(), Path.Combine(directoryElements.ToArray()));
            return pluginDirectory;
        }

        // Legacy method for backward compatibility - delegates to the new method
        private static string GetPluginDownloadDirectory(McpPlugin plugin, McpPluginVersion version)
        {
            return GetPluginWorkingDirectory(plugin, version);
        }
    }
}