// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.McpServer.Services;

namespace Microsoft.Greenlight.McpServer.Middleware;

/// <summary>
/// Middleware that resolves the effective user from an MCP session when JWT is absent.
/// Looks for the <c>X-MCP-Session</c> header, loads the session from cache, and attaches a claims principal.
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
    /// Invokes the middleware to resolve the user from the session if present.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="sessionManager">The session manager.</param>
    public async Task InvokeAsync(HttpContext context, IMcpSessionManager sessionManager)
    {
        // If already authenticated (JWT or secret middleware created a principal), skip.
        if (context.User?.Identity?.IsAuthenticated == true)
        {
             await _next(context);
            return;
        }

        try
        {
            var principal = await sessionManager.ResolvePrincipalFromHeadersAsync(context.Request.Headers, context.RequestAborted);
            if (principal != null)
            {
                context.User = principal;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve MCP session from headers");
        }

        await _next(context);
    }
}
