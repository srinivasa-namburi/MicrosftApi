using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Implementation of an MCP plugin using SSE (HTTP) communication.
    /// </summary>
#pragma warning disable SKEXP0001
    public class SseMcpPlugin : McpPluginBase, IDisposable
    {
        private readonly ILogger<SseMcpPlugin>? _logger;
        private bool _isInitialized;
        private bool _isDisposed;
        private bool _isStarted;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private DocumentProcessInfo? _initializedDocumentProcess;

        public override IMcpClient? McpClient { get; protected set; }

        public SseMcpPlugin(
            McpPluginManifest? manifest,
            string version,
            ILogger<SseMcpPlugin>? logger = null)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Name = manifest.Name;
            Description = manifest.Description;
            Version = version;
            Type = McpPluginType.Sse;
            _logger = logger;
        }

        public override async Task InitializeAsync(DocumentProcessInfo documentProcess)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SseMcpPlugin));

            if (_isInitialized && _initializedDocumentProcess?.Id == documentProcess.Id)
                return;

            await _lock.WaitAsync();
            try
            {
                if (_isInitialized && _initializedDocumentProcess?.Id == documentProcess.Id)
                    return;

                _logger?.LogInformation("Initializing SSE MCP plugin: {PluginName} v{Version} for document process: {ProcessName}",
                    Name, Version, documentProcess.ShortName);

                _lock.Release();
                await StartAsync();
                await _lock.WaitAsync();
                try
                {
                    if (_isDisposed)
                        throw new ObjectDisposedException(nameof(SseMcpPlugin));
                    _isInitialized = true;
                    _initializedDocumentProcess = documentProcess;
                    _logger?.LogInformation("SSE MCP plugin initialized: {PluginName} v{Version}", Name, Version);
                }
                finally { _lock.Release(); }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing SSE MCP plugin: {PluginName}", Name);
                if (_lock.CurrentCount == 0) _lock.Release();
                await StopAsync();
                throw;
            }
        }

        public override async Task<IList<KernelFunction>> GetKernelFunctionsAsync(DocumentProcessInfo documentProcess)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SseMcpPlugin));
            
            await InitializeAsync(documentProcess);
            
            if (McpClient == null)
            {
                _logger?.LogWarning("Unable to get kernel functions for SSE MCP plugin {PluginName} - MCP client is null", Name);
                return [];
            }
            
            // First attempt
            try
            {
                return await GetToolsAndCreateKernelFunctionsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error getting kernel functions from SSE MCP plugin {PluginName}. Attempting to reconnect...", Name);
                
                // Try to reinitialize the client and retry once
                try
                {
                    // Stop the current client
                    await StopAsync();
                    
                    // Start a new client
                    await StartAsync();
                    
                    if (McpClient == null)
                    {
                        _logger?.LogError("Failed to recreate MCP client for SSE plugin {PluginName}", Name);
                        return Array.Empty<KernelFunction>();
                    }
                    
                    // Retry with the new client
                    return await GetToolsAndCreateKernelFunctionsAsync();
                }
                catch (Exception retryEx)
                {
                    _logger?.LogError(retryEx, "Error getting kernel functions from SSE MCP plugin {PluginName} after reconnect attempt. Aborting.", Name);
                    return Array.Empty<KernelFunction>();
                }
            }
        }

        private async Task<IList<KernelFunction>> GetToolsAndCreateKernelFunctionsAsync()
        {
            var tools = await McpClient!.ListToolsAsync();
            if (tools.Count == 0)
            {
                _logger?.LogWarning("SSE MCP plugin {PluginName} has no tools available", Name);
                return [];
            }
            
            var kernelFunctions = tools
                .Select(tool =>
                {
                    var safeName = Regex.Replace(tool.Name, @"[^A-Za-z0-9_]", "_");
                    var renamedTool = tool.WithName(safeName);
                    return renamedTool.AsKernelFunction();
                })
                .ToList();
            
            _logger?.LogInformation("Retrieved {FunctionCount} kernel functions from SSE MCP plugin {PluginName}",
                kernelFunctions.Count, Name);
            
            foreach (var tool in tools)
            {
                _logger?.LogDebug("SSE MCP plugin {PluginName} provides tool: {ToolName}", Name, tool.Name);
            }
            
            return kernelFunctions;
        }

        public override async Task StartAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SseMcpPlugin));
            if (_isStarted)
                return;
            await _lock.WaitAsync();
            try
            {
                if (_isStarted)
                    return;
                _logger?.LogInformation("Starting SSE MCP plugin: {PluginName} v{Version}", Name, Version);
                string? url = Manifest?.Url;
                if (string.IsNullOrWhiteSpace(url))
                    throw new InvalidOperationException("SSE MCP plugin requires a URL.");
                var sseConfig = PrepareSseConfig(url);
                _lock.Release();
                try
                {
                    var mcpClient = await McpClientFactory.CreateAsync(new SseClientTransport(sseConfig));
                    await _lock.WaitAsync();
                    try
                    {
                        if (_isDisposed)
                        {
                            await mcpClient.DisposeAsync();
                            throw new ObjectDisposedException(nameof(SseMcpPlugin));
                        }
                        if (!_isStarted)
                        {
                            McpClient = mcpClient;
                            _isStarted = true;
                            _logger?.LogInformation("SSE MCP plugin started: {PluginName} v{Version}", Name, Version);
                        }
                        else
                        {
                            await mcpClient.DisposeAsync();
                        }
                    }
                    finally { _lock.Release(); }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error creating SSE MCP client for plugin: {PluginName}", Name);
                    await _lock.WaitAsync();
                    try { McpClient = null; } finally { _lock.Release(); }
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error starting SSE MCP plugin: {PluginName}", Name);
                McpClient = null;
                if (_lock.CurrentCount == 0) _lock.Release();
                throw;
            }
        }

        private SseClientTransportOptions PrepareSseConfig(string url)
        {
            var sseConfig = new SseClientTransportOptions
            {
                Name = Name,
                Endpoint = new Uri(url) // Convert string to Uri
            };
            // TODO: Add AzureCredentialHelper support if/when SseClientTransportOptions supports credentials
            return sseConfig;
        }

        public override async Task StopAsync()
        {
            if (_isDisposed)
                return;
            if (!_isStarted)
                return;
            await _lock.WaitAsync();
            try
            {
                if (!_isStarted)
                    return;
                _logger?.LogInformation("Stopping SSE MCP plugin: {PluginName}", Name);
                var client = McpClient;
                McpClient = null;
                _isStarted = false;
                _isInitialized = false;
                _initializedDocumentProcess = null;
                _lock.Release();
                if (client != null)
                {
                    try
                    {
                        await client.DisposeAsync();
                        _logger?.LogInformation("SSE MCP plugin stopped: {PluginName}", Name);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error disposing SSE MCP client for plugin: {PluginName}", Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping SSE MCP plugin: {PluginName}", Name);
                if (_lock.CurrentCount == 0) _lock.Release();
            }
        }

        public override async Task DisposeAsync()
        {
            if (_isDisposed)
                return;
            var acquiredLock = false;
            try
            {
                acquiredLock = await _lock.WaitAsync(TimeSpan.FromSeconds(5));
                if (!acquiredLock)
                {
                    _logger?.LogWarning("Could not acquire lock when disposing SSE MCP plugin: {PluginName}. Continuing with disposal.", Name);
                }
                _isDisposed = true;
                if (acquiredLock)
                {
                    _lock.Release();
                    acquiredLock = false;
                }
                await StopAsync();
                try
                {
                    _lock.Dispose();
                    _logger?.LogInformation("SSE MCP plugin disposed: {PluginName}", Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing lock for SSE MCP plugin: {PluginName}", Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing SSE MCP plugin: {PluginName}", Name);
                if (acquiredLock && _lock.CurrentCount == 0)
                {
                    _lock.Release();
                }
            }
        }

        public void Dispose()
        {
            DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }
    }
#pragma warning restore SKEXP0001
}
