// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.McpServer.Flow.Services;

namespace Microsoft.Greenlight.McpServer.Flow.Middleware;

/// <summary>
/// Middleware that resolves the effective user from an MCP session when JWT is absent.
/// Looks for the <c>X-MCP-Session</c> or <c>Mcp-Session-Id</c> header, loads the session from cache, and attaches a claims principal.
/// </summary>
public class McpSessionResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpSessionResolutionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpSessionResolutionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    /// <param name="logger">The logger.</param>
    public McpSessionResolutionMiddleware(RequestDelegate next, ILogger<McpSessionResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to resolve or create MCP session and populate McpRequestContext.
    /// If user is already authenticated (via JWT) and provides Mcp-Session-Id header,
    /// ensures the session exists in cache. Otherwise, tries to resolve user from existing session.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="requestContext">The scoped request context to populate.</param>
    public async Task InvokeAsync(HttpContext context, IMcpSessionManager sessionManager, McpRequestContext requestContext)
    {
        try
        {
            // Determine the server namespace based on port
            // This middleware only runs on backend servers (not on YARP proxy)
            // Flow backend (6007) and Business backend (6008) both see /* after YARP strips prefixes
            string serverNamespace;

            var localPort = context.Connection.LocalPort;
            _logger.LogDebug("McpSessionResolutionMiddleware: Detected local port {LocalPort}", localPort);

            if (localPort == 6007)
            {
                // Flow MCP server
                serverNamespace = "flow";
                _logger.LogDebug("McpSessionResolutionMiddleware: Setting namespace to 'flow' for port 6007");
            }
            else if (localPort == 6008)
            {
                // Business MCP server
                serverNamespace = "business";
                _logger.LogDebug("McpSessionResolutionMiddleware: Setting namespace to 'business' for port 6008");
            }
            else
            {
                // Fallback - should not happen in normal operation
                _logger.LogWarning("Unexpected port {Port} for MCP session resolution, defaulting to business namespace", localPort);
                serverNamespace = "business";
            }

            requestContext.ServerNamespace = serverNamespace;
            _logger.LogDebug("McpSessionResolutionMiddleware: ServerNamespace set to '{ServerNamespace}' for port {LocalPort}",
                requestContext.ServerNamespace, localPort);

            // Extract Greenlight session ID from X-Greenlight-Session header
            // This is OUR business session tracking, separate from MCP SDK's protocol session
            string? greenlightSessionId = null;
            if (context.Request.Headers.TryGetValue("X-Greenlight-Session", out var greenlightSession) &&
                !string.IsNullOrWhiteSpace(greenlightSession.FirstOrDefault()))
            {
                greenlightSessionId = greenlightSession.FirstOrDefault();
            }

            // Populate context with Greenlight session ID
            requestContext.GreenlightSessionId = greenlightSessionId;

            // If user is already authenticated (JWT), ensure their Greenlight session exists
            if (context.User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(greenlightSessionId))
            {
                var existingSession = await sessionManager.GetSessionAsync(serverNamespace, greenlightSessionId, context.RequestAborted);
                if (existingSession == null)
                {
                    // Create the session for this authenticated user
                    var providerSubjectId = sessionManager.ResolveProviderSubjectId(context.User);

                    if (!string.IsNullOrWhiteSpace(providerSubjectId))
                    {
                        // Store both initialization flag and ProviderSubjectId
                        await sessionManager.SetSessionDataAsync(serverNamespace, greenlightSessionId, "_initialized", "true", context.RequestAborted);
                        await sessionManager.SetProviderSubjectIdAsync(serverNamespace, greenlightSessionId, providerSubjectId, context.RequestAborted);
                        _logger.LogInformation("Created Greenlight session {GreenlightSessionId} in namespace {ServerNamespace} for authenticated user {ProviderSubjectId}",
                            greenlightSessionId, serverNamespace, providerSubjectId);

                        // Populate context
                        requestContext.ProviderSubjectId = providerSubjectId;
                        requestContext.User = context.User;
                    }
                }
                else
                {
                    // Session exists - populate context from session and user
                    requestContext.ProviderSubjectId = existingSession.ProviderSubjectId;
                    requestContext.User = context.User;
                }
            }
            // If not yet authenticated, try to resolve from existing Greenlight session
            else if (context.User?.Identity?.IsAuthenticated != true && !string.IsNullOrWhiteSpace(greenlightSessionId))
            {
                var principal = await sessionManager.ResolvePrincipalFromHeadersAsync(serverNamespace, context.Request.Headers, context.RequestAborted);
                if (principal != null)
                {
                    context.User = principal;
                    _logger.LogDebug("Resolved user from Greenlight session {GreenlightSessionId} in namespace {ServerNamespace}", greenlightSessionId, serverNamespace);

                    // Populate context
                    requestContext.User = principal;
                    requestContext.ProviderSubjectId = sessionManager.ResolveProviderSubjectId(principal);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve/create MCP session from headers");
        }

        await _next(context);
    }
}
