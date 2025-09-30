// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Greenlight.McpServer.Services;

namespace Microsoft.Greenlight.McpServer.Endpoints;

/// <summary>
/// Maps MCP session management endpoints under the /mcp route group.
/// </summary>
public static class McpSessionEndpoints
{
    /// <summary>
    /// Maps create, refresh, and invalidate session endpoints.
    /// </summary>
    /// <param name="group">The route group under /mcp.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder MapMcpSessionEndpoints(this RouteGroupBuilder group)
    {
        // POST /mcp/session — create
        group.MapPost("/session", async (HttpContext ctx, IMcpSessionManager manager) =>
        {
            if (ctx.User?.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var session = await manager.CreateAsync(ctx.User, ctx.RequestAborted);
            return Results.Json(new { sessionId = session.SessionId, expiresUtc = session.ExpiresUtc });
        }).WithName("CreateMcpSession");

        // POST /mcp/session/refresh
        group.MapPost("/session/refresh", async (HttpContext ctx, IMcpSessionManager manager) =>
        {
            var header = ctx.Request.Headers["X-MCP-Session"].FirstOrDefault();
            if (!Guid.TryParse(header, out var sessionId))
            {
                return Results.BadRequest(new { error = "Missing or invalid X-MCP-Session header" });
            }

            var (ok, session) = await manager.RefreshAsync(sessionId, ctx.RequestAborted);
            if (!ok || session is null)
            {
                return Results.NotFound(new { error = "Session not found" });
            }

            return Results.Json(new { sessionId, expiresUtc = session.ExpiresUtc });
        }).WithName("RefreshMcpSession");

        // DELETE /mcp/session
        group.MapDelete("/session", async (HttpContext ctx, IMcpSessionManager manager) =>
        {
            var header = ctx.Request.Headers["X-MCP-Session"].FirstOrDefault();
            if (!Guid.TryParse(header, out var sessionId))
            {
                return Results.BadRequest(new { error = "Missing or invalid X-MCP-Session header" });
            }

            await manager.InvalidateAsync(sessionId, ctx.RequestAborted);
            return Results.NoContent();
        }).WithName("InvalidateMcpSession");

        return group;
    }
}

