// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Implementation of an MCP plugin using stdio communication.
    /// </summary>
#pragma warning disable SKEXP0001
    public class StdioMcpPlugin : McpPluginBase, IDisposable
    {
        private readonly ILogger<StdioMcpPlugin>? _logger;
        private readonly string _workingDirectory;
        private bool _isInitialized;
        private bool _isDisposed;
        private bool _isStarted;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private DocumentProcessInfo? _initializedDocumentProcess;

        /// <summary>
        /// Gets the MCP client used to communicate with the MCP server.
        /// </summary>
        public override IMcpClient? McpClient { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StdioMcpPlugin"/> class.
        /// </summary>
        /// <param name="manifest">The MCP plugin manifest.</param>
        /// <param name="workingDirectory">The working directory for the MCP server.</param>
        /// <param name="version">The version of the MCP plugin.</param>
        /// <param name="logger">Optional logger.</param>
        public StdioMcpPlugin(
            McpPluginManifest? manifest,
            string workingDirectory,
            string version,
            ILogger<StdioMcpPlugin>? logger = null)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Name = manifest.Name;
            Description = manifest.Description;
            Version = version;
            Type = McpPluginType.Stdio;
            _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
            _logger = logger;
        }

        /// <summary>
        /// Initializes the MCP plugin asynchronously.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task InitializeAsync(DocumentProcessInfo documentProcess)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(StdioMcpPlugin));
            }

            // Quick check without the lock
            if (_isInitialized && _initializedDocumentProcess?.Id == documentProcess.Id)
            {
                return;
            }

            // Take the lock
            await _lock.WaitAsync();

            try
            {
                // Double-check after acquiring the lock
                if (_isInitialized && _initializedDocumentProcess?.Id == documentProcess.Id)
                {
                    return;
                }

                _logger?.LogInformation("Initializing MCP plugin: {PluginName} v{Version} for document process: {ProcessName}",
                    Name, Version, documentProcess.ShortName);

                // Release the lock before starting the plugin (which is an async operation)
                _lock.Release();

                // Start the process without holding the lock
                await StartAsync();

                // Take the lock again to update state
                await _lock.WaitAsync();
                try
                {
                    if (_isDisposed)
                    {
                        throw new ObjectDisposedException(nameof(StdioMcpPlugin));
                    }

                    _isInitialized = true;
                    _initializedDocumentProcess = documentProcess;
                    _logger?.LogInformation("MCP plugin initialized: {PluginName} v{Version}", Name, Version);
                }
                finally
                {
                    _lock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing MCP plugin: {PluginName}", Name);

                // Release the lock before async operation
                if (_lock.CurrentCount == 0)
                {
                    _lock.Release();
                }

                // Stop the plugin if initialization failed
                await StopAsync();
                throw;
            }
        }

        /// <summary>
        /// Gets the kernel functions for this MCP plugin.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>A task containing the list of kernel functions.</returns>
        public override async Task<IList<KernelFunction>> GetKernelFunctionsAsync(DocumentProcessInfo documentProcess)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(StdioMcpPlugin));
            }

            // Ensure plugin is initialized
            await InitializeAsync(documentProcess);

            if (McpClient == null)
            {
                _logger?.LogWarning("Unable to get kernel functions for MCP plugin {PluginName} - MCP client is null", Name);
                return [];
            }

            try
            {
                // Get the tools from the MCP client
                var tools = await McpClient.ListToolsAsync();

                if (tools.Count == 0)
                {
                    _logger?.LogWarning("MCP plugin {PluginName} has no tools available", Name);
                    return [];
                }

                // Convert tools to kernel functions and sanitize the tool names to match SK naming conventions
                var kernelFunctions = tools
                    .Select(tool =>
                    {
                        // 1) Turn e.g. "my-cool-tool" → "my_cool_tool"
                        var safeName = Regex.Replace(tool.Name, @"[^A-Za-z0-9_]", "_");
                        // 2) Get a new tool instance with the clean name
                        var renamedTool = tool.WithName(safeName);
                        // 3) Wrap it as a KernelFunction
                        return renamedTool.AsKernelFunction();
                    })
                    .ToList();

                _logger?.LogInformation("Retrieved {FunctionCount} kernel functions from MCP plugin {PluginName}",
                    kernelFunctions.Count, Name);

                // Log the individual tools
                foreach (var tool in tools)
                {
                    _logger?.LogDebug("MCP plugin {PluginName} provides tool: {ToolName}",
                        Name, tool.Name);
                }

                return kernelFunctions;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting kernel functions from MCP plugin {PluginName}", Name);
                return Array.Empty<KernelFunction>();
            }
        }

        /// <summary>
        /// Starts the MCP plugin.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task StartAsync()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(StdioMcpPlugin));
            }

            // Quick check without taking a lock 
            if (_isStarted)
            {
                return;
            }

            // Take the lock
            await _lock.WaitAsync();

            // All work is done in a separate method to ensure lock release in finally block
            try
            {
                // Double-check after acquiring the lock
                if (_isStarted)
                {
                    return;
                }

                // Prepare everything before async operations
                _logger?.LogInformation("Starting MCP plugin: {PluginName} v{Version}", Name, Version);

                // Determine the command path
                string commandPath = ResolveCommandPath();

                _logger?.LogDebug("MCP plugin command path: {CommandPath}", commandPath);
                _logger?.LogDebug("MCP plugin working directory: {WorkingDirectory}", _workingDirectory);

                // Create the configuration
                var stdioConfig = PrepareStdioConfig(commandPath);

                // Release the lock before the async operation
                _lock.Release();

                try
                {
                    // Create the MCP client using the StdioClientTransport - this is async and should run without holding the lock
                    var mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(stdioConfig));

                    // Take the lock again to update the state
                    await _lock.WaitAsync();
                    try
                    {
                        // Make sure we haven't been disposed or started in the meantime
                        if (_isDisposed)
                        {
                            await mcpClient.DisposeAsync();
                            throw new ObjectDisposedException(nameof(StdioMcpPlugin));
                        }

                        if (!_isStarted)
                        {
                            McpClient = mcpClient;
                            _isStarted = true;
                            _logger?.LogInformation("MCP plugin started: {PluginName} v{Version}", Name, Version);
                        }
                        else
                        {
                            // Another thread already started the plugin
                            await mcpClient.DisposeAsync();
                        }
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error creating MCP client for plugin: {PluginName}", Name);

                    // Take the lock again to update the state
                    await _lock.WaitAsync();
                    try
                    {
                        McpClient = null;
                    }
                    finally
                    {
                        _lock.Release();
                    }
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error starting MCP plugin: {PluginName}", Name);
                McpClient = null;

                // Make sure the lock is released
                if (_lock.CurrentCount == 0)
                {
                    _lock.Release();
                }
                throw;
            }
        }

        /// <summary>
        /// Resolves the command path based on the manifest command.
        /// </summary>
        /// <returns>The resolved command path.</returns>
        private string ResolveCommandPath()
        {
            if (Path.IsPathRooted(Manifest!.Command) ||
                IsExecutableInPath(Manifest!.Command))
            {
                // If the command is an absolute path or is in the system PATH
                return Manifest!.Command;
            }

            // If it's a relative path, combine it with the working directory
            return Path.GetFullPath(Path.Combine(_workingDirectory, Manifest!.Command));
        }

        /// <summary>
        /// Prepares the StdioClientTransport configuration.
        /// </summary>
        /// <param name="commandPath">The resolved command path.</param>
        /// <returns>The prepared configuration.</returns>
        private StdioClientTransportOptions PrepareStdioConfig(string commandPath)
        {
            var stdioConfig = new StdioClientTransportOptions
            {
                Name = Name,
                Command = commandPath,
                WorkingDirectory = _workingDirectory
            };

            // Handle arguments for StdioClientTransportOptions
            if (Manifest!.Arguments != null && Manifest.Arguments.Count > 0)
            {
                stdioConfig.Arguments = Manifest.Arguments.ToArray();
                _logger?.LogDebug("MCP plugin arguments: {Arguments}", string.Join(", ", Manifest.Arguments));
            }

            // Pass environment variables to the transport if present
            if (Manifest.EnvironmentVariables != null && Manifest.EnvironmentVariables.Count > 0)
            {
                stdioConfig.EnvironmentVariables = new Dictionary<string, string>(Manifest.EnvironmentVariables);
                _logger?.LogDebug("Set environment variables for plugin {PluginName}", Name);
            }

            return stdioConfig;
        }

        /// <summary>
        /// Stops the MCP plugin.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task StopAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            // Quick check without the lock
            if (!_isStarted)
            {
                return;
            }

            // Take the lock
            await _lock.WaitAsync();

            try
            {
                // Double-check after acquiring the lock
                if (!_isStarted)
                {
                    return;
                }

                _logger?.LogInformation("Stopping MCP plugin: {PluginName}", Name);

                // Capture the client reference before releasing the lock
                var client = McpClient;

                // Update state
                McpClient = null;
                _isStarted = false;
                _isInitialized = false;
                _initializedDocumentProcess = null;

                // Release the lock before async disposal
                _lock.Release();

                // Dispose the MCP client outside of the lock
                if (client != null)
                {
                    try
                    {
                        await client.DisposeAsync();
                        _logger?.LogInformation("MCP plugin stopped: {PluginName}", Name);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error disposing MCP client for plugin: {PluginName}", Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping MCP plugin: {PluginName}", Name);

                // Make sure the lock is released
                if (_lock.CurrentCount == 0)
                {
                    _lock.Release();
                }
            }
        }

        /// <summary>
        /// Disposes of the MCP plugin resources.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public override async Task DisposeAsync()
        {
            // Quick check without the lock
            if (_isDisposed)
            {
                return;
            }

            // Take the lock to update the state
            var acquiredLock = false;
            try
            {
                // Use TryWait with a short timeout to avoid deadlocks during disposal
                acquiredLock = await _lock.WaitAsync(TimeSpan.FromSeconds(5));
                if (!acquiredLock)
                {
                    _logger?.LogWarning("Could not acquire lock when disposing MCP plugin: {PluginName}. Continuing with disposal.", Name);
                }

                _isDisposed = true;

                if (acquiredLock)
                {
                    _lock.Release();
                    acquiredLock = false;
                }

                // Stop the plugin (which has its own locking logic)
                await StopAsync();

                // Dispose the lock
                try
                {
                    _lock.Dispose();
                    _logger?.LogInformation("MCP plugin disposed: {PluginName}", Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing lock for MCP plugin: {PluginName}", Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing MCP plugin: {PluginName}", Name);

                if (acquiredLock && _lock.CurrentCount == 0)
                {
                    _lock.Release();
                }
            }
        }

        /// <summary>
        /// Disposes of the MCP plugin resources.
        /// </summary>
        public void Dispose()
        {
            // Use ConfigureAwait(false) to avoid deadlocks when disposing synchronously
            DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Checks if the executable is in the system PATH.
        /// </summary>
        /// <param name="executableName">The name of the executable.</param>
        /// <returns>True if the executable is in the PATH, otherwise false.</returns>
        private static bool IsExecutableInPath(string executableName)
        {
            // Common commands that don't need path resolution
            var commonCommands = new[] { "npx", "node", "npm", "dotnet", "python", "python3", "pwsh", "powershell" };

            if (commonCommands.Contains(executableName, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            // On Windows, executables can have extensions like .exe, .cmd, .bat
            var pathExtensions = new List<string>();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
                if (!string.IsNullOrEmpty(pathExt))
                {
                    pathExtensions.AddRange(pathExt.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
                }
            }

            // Get the system PATH
            var systemPath = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(systemPath))
            {
                return false;
            }

            var paths = systemPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var path in paths)
            {
                // Check for the executable directly
                var filePath = Path.Combine(path, executableName);
                if (File.Exists(filePath))
                {
                    return true;
                }

                // On Windows, check with extensions
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    foreach (var ext in pathExtensions)
                    {
                        var filePathWithExt = Path.Combine(path, executableName + ext);
                        if (File.Exists(filePathWithExt))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
