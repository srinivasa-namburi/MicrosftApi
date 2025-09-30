// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Greenlight.McpServer.Middleware;

/// <summary>
/// Middleware that extracts the session ID from MCP request body and injects it as a header.
/// This bridges the gap between MCP protocol (session in body) and HTTP transport (session in header).
/// </summary>
public class McpSessionInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpSessionInjectionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpSessionInjectionMiddleware"/> class.
    /// </summary>
    public McpSessionInjectionMiddleware(RequestDelegate next, ILogger<McpSessionInjectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes the HTTP request to extract and inject MCP session ID.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Only process POST requests to MCP endpoints that might contain tool calls
        if (context.Request.Method == HttpMethod.Post.Method &&
            (context.Request.Path.StartsWithSegments("/mcp") ||
             context.Request.Path.StartsWithSegments("/flow")))
        {
            // Enable buffering so we can read the body multiple times
            context.Request.EnableBuffering();

            try
            {
                // Read the request body
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0; // Reset for downstream middleware

                // Try to extract sessionId from the JSON body
                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);

                        // MCP tool calls come in as JSON-RPC with params containing the arguments
                        if (doc.RootElement.TryGetProperty("params", out var paramsElement))
                        {
                            // Check if params has arguments with sessionId
                            if (paramsElement.TryGetProperty("arguments", out var argsElement))
                            {
                                if (argsElement.TryGetProperty("sessionId", out var sessionIdElement))
                                {
                                    var sessionId = sessionIdElement.GetString();
                                    if (!string.IsNullOrEmpty(sessionId))
                                    {
                                        // Inject as X-MCP-Session header for downstream middleware
                                        context.Request.Headers["X-MCP-Session"] = sessionId;

                                        // Also add as Mcp-Session-Id for standard MCP HTTP transport
                                        context.Request.Headers["Mcp-Session-Id"] = sessionId;

                                        _logger.LogDebug("Injected MCP session ID from request body into headers");
                                    }
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogDebug(ex, "Could not parse request body as JSON for session extraction");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting MCP session ID from request body");
            }
        }

        await _next(context);
    }
}