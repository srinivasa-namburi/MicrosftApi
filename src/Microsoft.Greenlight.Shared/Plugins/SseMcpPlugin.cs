// Copyright (c) Microsoft Corporation. All rights reserved.
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// HTTP-first MCP plugin implementation with support for Streaming HTTP and SSE fallback.
    /// - Uses Streaming HTTP transport by default (Authorization via HttpClient).
    /// - Falls back to SSE when URL explicitly targets an SSE endpoint or HTTP fails with 404/406.
    /// </summary>
#pragma warning disable SKEXP0001
    public class HttpMcpPlugin : McpPluginBase, IDisposable
    {
        private readonly ILogger? _logger;
        private readonly AzureCredentialHelper? _credentialHelper;
        private readonly IConfiguration? _configuration;
        private readonly Orleans.IClusterClient? _clusterClient;
        private bool _isInitialized;
        private bool _isDisposed;
        private bool _isStarted;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private DocumentProcessInfo? _initializedDocumentProcess;

        /// <summary>
        /// Gets the underlying MCP client instance, if started.
        /// </summary>
        public override IMcpClient? McpClient { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpMcpPlugin"/> class.
        /// </summary>
        public HttpMcpPlugin(
            McpPluginManifest? manifest,
            string version,
            ILogger? logger = null,
            AzureCredentialHelper? credentialHelper = null,
            IConfiguration? configuration = null,
            Orleans.IClusterClient? clusterClient = null)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Name = manifest.Name;
            Description = manifest.Description;
            Version = version;
            Type = McpPluginType.Http;
            _logger = logger;
            _credentialHelper = credentialHelper;
            _configuration = configuration;
            _clusterClient = clusterClient;
        }

        /// <summary>
        /// Initializes the plugin for a document process.
        /// </summary>
        public override async Task InitializeAsync(DocumentProcessInfo documentProcess)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(HttpMcpPlugin));

            if (_isInitialized && _initializedDocumentProcess?.Id == documentProcess.Id)
                return;

            await _lock.WaitAsync();
            try
            {
                if (_isInitialized && _initializedDocumentProcess?.Id == documentProcess.Id)
                    return;

                _logger?.LogInformation("Initializing HTTP MCP plugin: {PluginName} v{Version} for document process: {ProcessName}",
                    Name, Version, documentProcess.ShortName);

                _lock.Release();
                await StartAsync();
                await _lock.WaitAsync();
                try
                {
                    if (_isDisposed)
                        throw new ObjectDisposedException(nameof(HttpMcpPlugin));
                    _isInitialized = true;
                    _initializedDocumentProcess = documentProcess;
                    _logger?.LogInformation("HTTP MCP plugin initialized: {PluginName} v{Version}", Name, Version);
                }
                finally { _lock.Release(); }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing HTTP MCP plugin: {PluginName}", Name);
                if (_lock.CurrentCount == 0) _lock.Release();
                await StopAsync();
                throw;
            }
        }

        /// <summary>
        /// Retrieves the kernel functions exposed by the connected MCP server.
        /// </summary>
        public override async Task<IList<KernelFunction>> GetKernelFunctionsAsync(DocumentProcessInfo documentProcess)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SseMcpPlugin));
            await InitializeAsync(documentProcess);
            if (McpClient == null)
            {
                _logger?.LogWarning("Unable to get kernel functions for HTTP MCP plugin {PluginName} - MCP client is null", Name);
                return [];
            }
            // First attempt
            try
            {
                return await GetToolsAndCreateKernelFunctionsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error getting kernel functions from HTTP MCP plugin {PluginName}. Attempting to reconnect...", Name);
                // Try to reinitialize the client and retry once
                try
                {
                    // Stop the current client
                    await StopAsync();
                    // Start a new client
                    await StartAsync();
                    if (McpClient == null)
                    {
                        _logger?.LogError("Failed to recreate MCP client for HTTP plugin {PluginName}", Name);
                        return Array.Empty<KernelFunction>();
                    }
                    // Retry with the new client
                    return await GetToolsAndCreateKernelFunctionsAsync();
                }
                catch (Exception retryEx)
                {
                    _logger?.LogError(retryEx, "Error getting kernel functions from HTTP MCP plugin {PluginName} after reconnect attempt. Aborting.", Name);
                    return Array.Empty<KernelFunction>();
                }
            }
        }

        private async Task<IList<KernelFunction>> GetToolsAndCreateKernelFunctionsAsync()
        {
            var tools = await McpClient!.ListToolsAsync();
            if (tools.Count == 0)
            {
                _logger?.LogWarning("HTTP MCP plugin {PluginName} has no tools available", Name);
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

            _logger?.LogInformation("Retrieved {FunctionCount} kernel functions from HTTP MCP plugin {PluginName}",
                kernelFunctions.Count, Name);

            foreach (var tool in tools)
            {
                _logger?.LogDebug("HTTP MCP plugin {PluginName} provides tool: {ToolName}", Name, tool.Name);
            }

            return kernelFunctions;
        }

        /// <inheritdoc/>
        /// <summary>
        /// Starts the plugin by connecting to the MCP server using Streamable HTTP (preferred) or SSE fallback.
        /// </summary>
        public override async Task StartAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(HttpMcpPlugin));
            if (_isStarted)
                return;
            await _lock.WaitAsync();
            try
            {
                if (_isStarted)
                    return;
                _logger?.LogInformation("Starting HTTP MCP plugin: {PluginName} v{Version}", Name, Version);
                string? url = Manifest?.Url;
                if (string.IsNullOrWhiteSpace(url))
                    throw new InvalidOperationException("HTTP MCP plugin requires a URL.");
                var baseUri = new Uri(url);
                _lock.Release();
                try
                {
                    // Decide transport: explicit "/sse" path => SSE; otherwise prefer HTTP Streaming
                    bool explicitSse = baseUri.AbsolutePath.Contains("/sse", StringComparison.OrdinalIgnoreCase);

                    // Build endpoint candidates
                    var httpEndpointsToTry = new List<Uri>();
                    var sseEndpointsToTry = new List<Uri>();

                    if (explicitSse)
                    {
                        sseEndpointsToTry.Add(baseUri);
                    }
                    else
                    {
                        // Prefer base URL as-is (may already be /mcp)
                        httpEndpointsToTry.Add(baseUri);
                        // Also try ensuring /mcp suffix
                        if (!baseUri.AbsolutePath.TrimEnd('/').EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
                        {
                            httpEndpointsToTry.Add(new Uri(AppendPath(baseUri, "mcp")));
                        }
                        // Keep SSE fallbacks for backwards-compat
                        sseEndpointsToTry.Add(new Uri(AppendPath(baseUri, "mcp/sse")));
                        sseEndpointsToTry.Add(new Uri(AppendPath(baseUri, "sse")));
                    }

                    Exception? lastException = null;
                    // Try HTTP streaming endpoints first (if not explicitly SSE)
                    foreach (var endpoint in httpEndpointsToTry)
                    {
                        try
                        {
                            var httpClient = await CreateHttpClientAsync();
                            var httpOptions = new SseClientTransportOptions
                            {
                                Name = Name,
                                Endpoint = endpoint,
                                TransportMode = HttpTransportMode.StreamableHttp
                            };
                            var transport = new SseClientTransport(httpOptions, httpClient, loggerFactory: null, ownsHttpClient: true);
                            var mcpClient = await McpClientFactory.CreateAsync(transport);
                            await _lock.WaitAsync();
                            try
                            {
                                if (_isDisposed)
                                {
                                    await mcpClient.DisposeAsync();
                                    throw new ObjectDisposedException(nameof(HttpMcpPlugin));
                                }
                                if (!_isStarted)
                                {
                                    McpClient = mcpClient;
                                    _isStarted = true;
                                    _logger?.LogInformation("HTTP MCP plugin started: {PluginName} v{Version} (Endpoint: {Endpoint})", Name, Version, endpoint);
                                    return;
                                }
                                else
                                {
                                    await mcpClient.DisposeAsync();
                                }
                            }
                            finally { _lock.Release(); }
                        }
                        catch (Exception ex) when (Is404or406(ex))
                        {
                            _logger?.LogInformation(ex, "Endpoint {Endpoint} failed with 404/406. Trying next fallback.", endpoint);
                            lastException = ex;
                            continue;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error creating HTTP MCP client for plugin: {PluginName} (Endpoint: {Endpoint})", Name, endpoint);
                            lastException = ex;
                            // On generic failure, try SSE fallbacks next
                            break;
                        }
                    }

                    // If we get here and were not explicitly SSE, try SSE fallbacks for back-compat
                    if (!explicitSse && !_isStarted)
                    {
                        foreach (var endpoint in sseEndpointsToTry)
                        {
                            try
                            {
                                var httpClient = await CreateHttpClientAsync();
                                var tryConfig = new SseClientTransportOptions
                                {
                                    Name = Name,
                                    Endpoint = endpoint,
                                    TransportMode = HttpTransportMode.Sse
                                };
                                var transport = new SseClientTransport(tryConfig, httpClient, loggerFactory: null, ownsHttpClient: true);
                                var mcpClient = await McpClientFactory.CreateAsync(transport);
                                await _lock.WaitAsync();
                                try
                                {
                                    if (_isDisposed)
                                    {
                                        await mcpClient.DisposeAsync();
                                        throw new ObjectDisposedException(nameof(HttpMcpPlugin));
                                    }
                                    if (!_isStarted)
                                    {
                                        McpClient = mcpClient;
                                        _isStarted = true;
                                        _logger?.LogInformation("HTTP MCP plugin (SSE fallback) started: {PluginName} v{Version} (Endpoint: {Endpoint})", Name, Version, endpoint);
                                        return;
                                    }
                                    else
                                    {
                                        await mcpClient.DisposeAsync();
                                    }
                                }
                                finally { _lock.Release(); }
                            }
                            catch (Exception ex) when (Is404or406(ex))
                            {
                                _logger?.LogInformation(ex, "SSE fallback endpoint {Endpoint} failed with 404/406. Trying next fallback.", endpoint);
                                lastException = ex;
                                continue;
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error creating SSE MCP client (fallback) for plugin: {PluginName} (Endpoint: {Endpoint})", Name, endpoint);
                                lastException = ex;
                                break;
                            }
                        }
                    }
                    await _lock.WaitAsync();
                    try { McpClient = null; } finally { _lock.Release(); }
                    throw lastException ?? new InvalidOperationException("Failed to create HTTP/SSE MCP client for all endpoints.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error starting HTTP MCP plugin: {PluginName}", Name);
                    McpClient = null;
                    if (_lock.CurrentCount == 0) _lock.Release();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error starting HTTP MCP plugin: {PluginName}", Name);
                McpClient = null;
                if (_lock.CurrentCount == 0) _lock.Release();
                throw;
            }
        }

        /// <summary>
        /// Appends a path segment to a base URI, handling trailing slashes.
        /// </summary>
        private static string AppendPath(Uri baseUri, string segment)
        {
            var baseStr = baseUri.ToString().TrimEnd('/');
            return $"{baseStr}/{segment}";
        }

        /// <summary>
        /// Determines if the exception is an HTTP 404 or 406 error (direct or inner exception).
        /// </summary>
        private static bool Is404or406(Exception ex)
        {
            // Check for common HTTP exception types and status codes
            var type = ex.GetType();
            if (type.Name == "TransportException")
            {
                var statusCodeProp = type.GetProperty("StatusCode");
                if (statusCodeProp != null)
                {
                    var statusCode = statusCodeProp.GetValue(ex);
                    if (statusCode is int code && (code == 404 || code == 406))
                        return true;
                }
            }
            if (ex.Message != null && (ex.Message.Contains("404") || ex.Message.Contains("406")))
                return true;
            if (ex.InnerException != null)
                return Is404or406(ex.InnerException);
            return false;
        }

        /// <summary>
        /// Creates an HttpClient configured with Authorization if required.
        /// Builds a fresh instance to ensure token freshness for HTTP transports.
        /// </summary>
        private async Task<HttpClient> CreateHttpClientAsync(string? providerSubjectId = null, CancellationToken cancellationToken = default)
        {
            var httpClient = new HttpClient();

            var authType = Manifest?.AuthenticationType;
            if (authType == null || authType == McpPluginAuthenticationType.None)
            {
                return httpClient; // no auth
            }

            try
            {
                switch (authType)
                {
                    case McpPluginAuthenticationType.GreenlightManagedIdentity:
                        if (_credentialHelper != null)
                        {
                            var credential = _credentialHelper.GetAzureCredential();
                            var audience = _configuration?["Mcp:Audience"] ?? _configuration?["AzureAd:Audience"];
                            if (!string.IsNullOrWhiteSpace(audience))
                            {
                                // Support either bare resource or explicit scope
                                var scope = audience!.EndsWith("/.default", StringComparison.OrdinalIgnoreCase)
                                    ? audience!
                                    : audience!.TrimEnd('/') + "/.default";
                                var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken).ConfigureAwait(false);
                                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
                            }
                            else
                            {
                                _logger?.LogWarning("ManagedIdentity auth requested but no Mcp:Audience/AzureAd:Audience configured. Proceeding without Authorization header.");
                            }
                        }
                        else
                        {
                            _logger?.LogWarning("ManagedIdentity auth requested but AzureCredentialHelper is not available.");
                        }
                        break;
                    case McpPluginAuthenticationType.UserBearerToken:
                        // Allow ambient context to provide the ProviderSubjectId if not explicitly provided
                        providerSubjectId ??= Microsoft.Greenlight.Shared.Services.UserExecutionContext.ProviderSubjectId;
                        if (string.IsNullOrWhiteSpace(providerSubjectId))
                        {
                            _logger?.LogWarning("UserBearerToken auth requested but no ProviderSubjectId was supplied. Skipping Authorization header.");
                            break;
                        }
                        if (_clusterClient != null)
                        {
                            var grain = _clusterClient.GetGrain<Microsoft.Greenlight.Grains.Shared.Contracts.IUserTokenStoreGrain>(providerSubjectId);
                            var token = await grain.GetTokenAsync().ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(token?.AccessToken))
                            {
                                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token!.AccessToken);
                            }
                            else
                            {
                                _logger?.LogWarning("No access token found in UserTokenStore for ProviderSubjectId={ProviderSubjectId}", providerSubjectId);
                            }
                        }
                        else
                        {
                            _logger?.LogWarning("UserBearerToken auth requested but Orleans IClusterClient is not available.");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting Authorization header for HTTP MCP plugin {PluginName}", Name);
            }

            return httpClient;
        }

        // Note: No HttpClientTransportOptions; SseClientTransportOptions is used for both Streamable HTTP and SSE.

        /// <summary>
        /// Stops the plugin and disposes the MCP client.
        /// </summary>
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
                _logger?.LogInformation("Stopping HTTP MCP plugin: {PluginName}", Name);
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
                        _logger?.LogInformation("HTTP MCP plugin stopped: {PluginName}", Name);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error disposing HTTP MCP client for plugin: {PluginName}", Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping HTTP MCP plugin: {PluginName}", Name);
                if (_lock.CurrentCount == 0) _lock.Release();
            }
        }

        /// <summary>
        /// Asynchronously disposes the plugin and its resources.
        /// </summary>
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
                    _logger?.LogWarning("Could not acquire lock when disposing HTTP MCP plugin: {PluginName}. Continuing with disposal.", Name);
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
                    _logger?.LogInformation("HTTP MCP plugin disposed: {PluginName}", Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing lock for HTTP MCP plugin: {PluginName}", Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing HTTP MCP plugin: {PluginName}", Name);
                if (acquiredLock && _lock.CurrentCount == 0)
                {
                    _lock.Release();
                }
            }
        }

        /// <summary>
        /// Disposes the plugin synchronously.
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }
    }
#pragma warning restore SKEXP0001
}

// Back-compat type alias: existing references to SseMcpPlugin will now use HttpMcpPlugin behavior
namespace Microsoft.Greenlight.Shared.Plugins
{
#pragma warning disable SKEXP0001
    /// <summary>
    /// Backward-compatible SSE plugin type alias that uses the new HTTP-capable implementation.
    /// </summary>
    public class SseMcpPlugin : HttpMcpPlugin
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SseMcpPlugin"/> class.
        /// </summary>
        public SseMcpPlugin(
            McpPluginManifest? manifest,
            string version,
            ILogger<SseMcpPlugin>? logger = null,
            AzureCredentialHelper? credentialHelper = null,
            IConfiguration? configuration = null,
            Orleans.IClusterClient? clusterClient = null)
            : base(manifest, version, logger, credentialHelper, configuration, clusterClient)
        {
        }
    }
#pragma warning restore SKEXP0001
}
