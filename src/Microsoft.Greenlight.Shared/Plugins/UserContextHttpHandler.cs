// Copyright (c) Microsoft Corporation. All rights reserved.
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using System.Net.Http.Headers;

namespace Microsoft.Greenlight.Shared.Plugins;

/// <summary>
/// Delegating handler that injects user authentication at request time based on ambient UserExecutionContext.
/// This allows MCP plugin HttpClients to use the current user's identity even when the client is created
/// before the user context is available.
/// </summary>
public class UserContextHttpHandler : DelegatingHandler
{
    private readonly McpPluginAuthenticationType _authenticationType;
    private readonly ILogger? _logger;
    private readonly AzureCredentialHelper? _credentialHelper;
    private readonly IConfiguration? _configuration;
    private readonly Orleans.IClusterClient? _clusterClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserContextHttpHandler"/> class.
    /// </summary>
    /// <param name="authenticationType">The authentication type for this MCP plugin.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="credentialHelper">Optional Azure credential helper for managed identity auth.</param>
    /// <param name="configuration">Optional configuration for audience/scope resolution.</param>
    /// <param name="clusterClient">Optional Orleans cluster client for user token resolution.</param>
    /// <param name="innerHandler">Optional inner handler. If not provided, a new HttpClientHandler will be created.</param>
    public UserContextHttpHandler(
        McpPluginAuthenticationType authenticationType,
        ILogger? logger = null,
        AzureCredentialHelper? credentialHelper = null,
        IConfiguration? configuration = null,
        Orleans.IClusterClient? clusterClient = null,
        HttpMessageHandler? innerHandler = null)
        : base(innerHandler ?? new HttpClientHandler()) // Call base constructor with provided or new inner handler
    {
        _authenticationType = authenticationType;
        _logger = logger;
        _credentialHelper = credentialHelper;
        _configuration = configuration;
        _clusterClient = clusterClient;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Check if we need to add authentication
        if (_authenticationType != McpPluginAuthenticationType.None && request.Headers.Authorization == null)
        {
            try
            {
                var authHeader = await GetAuthorizationHeaderAsync(cancellationToken);
                if (authHeader != null)
                {
                    request.Headers.Authorization = authHeader;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to add Authorization header to MCP request");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets the authorization header based on the configured authentication type and current user context.
    /// </summary>
    private async Task<AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(CancellationToken cancellationToken)
    {
        switch (_authenticationType)
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
                        var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken);
                        return new AuthenticationHeaderValue("Bearer", token.Token);
                    }
                    else
                    {
                        _logger?.LogWarning("ManagedIdentity auth requested but no Mcp:Audience/AzureAd:Audience configured");
                    }
                }
                else
                {
                    _logger?.LogWarning("ManagedIdentity auth requested but AzureCredentialHelper is not available");
                }
                break;

            case McpPluginAuthenticationType.UserBearerToken:
                // Read from ambient user execution context (set by ProviderSubjectInjectionFilter during function invocation)
                var providerSubjectId = UserExecutionContext.ProviderSubjectId;
                if (string.IsNullOrWhiteSpace(providerSubjectId))
                {
                    _logger?.LogDebug("UserBearerToken auth requested but UserExecutionContext.ProviderSubjectId is not set");
                    break;
                }

                if (_clusterClient != null)
                {
                    var grain = _clusterClient.GetGrain<Microsoft.Greenlight.Grains.Shared.Contracts.IUserTokenStoreGrain>(providerSubjectId);
                    var token = await grain.GetTokenAsync();
                    if (!string.IsNullOrWhiteSpace(token?.AccessToken))
                    {
                        _logger?.LogDebug("Adding user bearer token for ProviderSubjectId={ProviderSubjectId}", providerSubjectId);
                        return new AuthenticationHeaderValue("Bearer", token!.AccessToken);
                    }
                    else
                    {
                        _logger?.LogWarning("No access token found in UserTokenStore for ProviderSubjectId={ProviderSubjectId}", providerSubjectId);
                    }
                }
                else
                {
                    _logger?.LogWarning("UserBearerToken auth requested but Orleans IClusterClient is not available");
                }
                break;
        }

        return null;
    }
}
