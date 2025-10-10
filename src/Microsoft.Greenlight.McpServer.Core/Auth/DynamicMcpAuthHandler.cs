// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;

namespace Microsoft.Greenlight.McpServer.Core.Auth;

/// <summary>
/// Authorization requirement for MCP endpoints that respects dynamic configuration.
/// </summary>
public class DynamicMcpAuthRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Authorization handler that enforces MCP authentication based on current configuration values.
/// Supports four authentication modes:
/// 1. Open (DisableAuth=true, SecretEnabled=false): No auth required
/// 2. Secret-only (DisableAuth=true, SecretEnabled=true): Shared secret required
/// 3. JWT or Secret (DisableAuth=false, SecretEnabled=true): Either JWT or secret required
/// 4. JWT-only (DisableAuth=false, SecretEnabled=false): Per-user JWT required
/// </summary>
public class DynamicMcpAuthHandler : AuthorizationHandler<DynamicMcpAuthRequirement>
{
    private readonly IOptionsMonitor<ServiceConfigurationOptions.McpOptions> _mcpOptions;
    private readonly ILogger<DynamicMcpAuthHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicMcpAuthHandler"/> class.
    /// </summary>
    /// <param name="mcpOptions">MCP configuration options monitor.</param>
    /// <param name="logger">Logger instance.</param>
    public DynamicMcpAuthHandler(
        IOptionsMonitor<ServiceConfigurationOptions.McpOptions> mcpOptions,
        ILogger<DynamicMcpAuthHandler> logger)
    {
        _mcpOptions = mcpOptions;
        _logger = logger;
    }

    /// <summary>
    /// Makes the authorization decision based on current configuration and authentication state.
    /// </summary>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DynamicMcpAuthRequirement requirement)
    {
        var opts = _mcpOptions.CurrentValue;
        var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
        var authType = context.User?.Identity?.AuthenticationType;

        // Mode 1: Open access (no per-user auth required, no secrets required)
        if (opts.DisableAuth && !opts.SecretEnabled)
        {
            _logger.LogDebug("MCP auth mode: Open (no authentication required)");
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Mode 2: Secret-only (no per-user auth required, but shared secret required)
        // Secret middleware will have already validated and set principal with AuthenticationType="McpSecret"
        if (opts.DisableAuth && opts.SecretEnabled)
        {
            if (isAuthenticated && authType == "McpSecret")
            {
                _logger.LogDebug("MCP auth mode: Secret-only (authenticated via secret)");
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("MCP auth mode: Secret-only (missing or invalid secret)");
                // Don't succeed - will result in 403
            }
            return Task.CompletedTask;
        }

        // Mode 3 & 4: Per-user auth enabled
        // Mode 3: JWT or Secret (DisableAuth=false, SecretEnabled=true)
        // Mode 4: JWT-only (DisableAuth=false, SecretEnabled=false)
        if (!opts.DisableAuth)
        {
            if (isAuthenticated)
            {
                // Accept either JWT or secret authentication
                var mode = opts.SecretEnabled ? "JWT or Secret" : "JWT-only";
                _logger.LogDebug("MCP auth mode: {Mode} (authenticated via {AuthType})", mode, authType);
                context.Succeed(requirement);
            }
            else
            {
                var mode = opts.SecretEnabled ? "JWT or Secret" : "JWT-only";
                _logger.LogWarning("MCP auth mode: {Mode} (not authenticated)", mode);
                // Don't succeed - will result in 403
            }
            return Task.CompletedTask;
        }

        // Fallback - should not reach here
        _logger.LogWarning("MCP auth: Unexpected configuration state (DisableAuth={DisableAuth}, SecretEnabled={SecretEnabled})",
            opts.DisableAuth, opts.SecretEnabled);
        return Task.CompletedTask;
    }
}
