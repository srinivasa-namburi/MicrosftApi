// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.McpServer.Middleware;
using Microsoft.Greenlight.McpServer.Models;
using Microsoft.Greenlight.McpServer.Services;

namespace Microsoft.Greenlight.McpServer.Tests;

public class McpSessionResolutionMiddlewareTests
{
    private sealed class FakeSessionManager : IMcpSessionManager
    {
        private readonly Dictionary<Guid, McpSession> _sessions = new();

        public Task<McpSession> CreateAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid();
            var session = new McpSession
            {
                SessionId = id,
                UserObjectId = user.FindFirst("oid")?.Value ?? "",
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
            };
            _sessions[id] = session;
            return Task.FromResult(session);
        }

        public Task<(bool Ok, McpSession? Session)> RefreshAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            if (_sessions.TryGetValue(sessionId, out var s))
            {
                s.ExpiresUtc = DateTime.UtcNow.AddMinutes(30);
                return Task.FromResult(((bool Ok, McpSession? Session))(true, s));
            }
            return Task.FromResult(((bool Ok, McpSession? Session))(false, null));
        }

        public Task InvalidateAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            _sessions.Remove(sessionId);
            return Task.CompletedTask;
        }

        public Task<List<McpSession>> ListAsync(CancellationToken cancellationToken)
        {
            var sessions = _sessions.Values.ToList();
            return Task.FromResult(sessions);
        }

        public Task<ClaimsPrincipal?> ResolvePrincipalFromHeadersAsync(IHeaderDictionary headers, CancellationToken cancellationToken)
        {
            if (!headers.TryGetValue("X-MCP-Session", out var values))
            {
                return Task.FromResult<ClaimsPrincipal?>(null);
            }
            var raw = values.FirstOrDefault();
            if (!Guid.TryParse(raw, out var id))
            {
                return Task.FromResult<ClaimsPrincipal?>(null);
            }
            if (_sessions.TryGetValue(id, out var s))
            {
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, s.UserObjectId),
                    new Claim("oid", s.UserObjectId)
                }, "McpSession");
                return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
            }
            return Task.FromResult<ClaimsPrincipal?>(null);
        }

        public void Seed(Guid id, McpSession value) => _sessions[id] = value;

        public Task<Guid?> GetOrCreateFlowSessionAsync(string mcpSessionId, ClaimsPrincipal? user, CancellationToken cancellationToken)
        {
            // For tests, just return a new GUID
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        return ctx;
    }

    [Fact]
    public async Task ResolvesUser_FromValidSessionHeader()
    {
        var sessionId = Guid.NewGuid();
        var cacheKey = $"mcp:sessions:{sessionId}";
        var session = new McpSession
        {
            SessionId = sessionId,
            UserObjectId = "00000000-0000-0000-0000-000000000002",
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
        };

        var manager = new FakeSessionManager();
        manager.Seed(sessionId, session);

        using var lf = LoggerFactory.Create(builder => { });
        var calledNext = false;
        var middleware = new McpSessionResolutionMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSessionResolutionMiddleware>());

        var context = CreateContext("/mcp/tools");
        context.Request.Headers["X-MCP-Session"] = sessionId.ToString();

        await middleware.InvokeAsync(context, manager);

        Assert.True(calledNext);
        Assert.True(context.User?.Identity?.IsAuthenticated == true);
        Assert.Equal("McpSession", context.User?.Identity?.AuthenticationType);
        Assert.Equal("00000000-0000-0000-0000-000000000002", context.User?.FindFirst("oid")?.Value);
    }

    [Fact]
    public async Task Skips_WhenNoHeaderOrSessionNotFound()
    {
        using var lf = LoggerFactory.Create(builder => { });
        var calledNext = false;
        var middleware = new McpSessionResolutionMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSessionResolutionMiddleware>());

        var manager = new FakeSessionManager();

        var context = CreateContext("/mcp/tools");
        await middleware.InvokeAsync(context, manager);

        Assert.True(calledNext);
        Assert.False(context.User?.Identity?.IsAuthenticated == true);
        Assert.Null(context.User?.FindFirst("oid"));
    }

    [Fact]
    public async Task Skips_WhenAlreadyAuthenticated()
    {
        using var lf = LoggerFactory.Create(builder => { });
        var calledNext = false;
        var middleware = new McpSessionResolutionMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSessionResolutionMiddleware>());

        var manager = new FakeSessionManager();

        var context = CreateContext("/mcp/tools");
        // Pre-authenticate via JWT
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "jwt-user") }, "Jwt"));
        context.Request.Headers["X-MCP-Session"] = Guid.NewGuid().ToString();

        await middleware.InvokeAsync(context, manager);

        Assert.True(calledNext);
        Assert.Equal("Jwt", context.User?.Identity?.AuthenticationType);
    }

    [Fact]
    public async Task HandlesExpiredSession_ContinuesExecution()
    {
        using var lf = LoggerFactory.Create(builder => { });
        var calledNext = false;
        var middleware = new McpSessionResolutionMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSessionResolutionMiddleware>());

        var manager = new FakeSessionManager();
        var sessionId = Guid.NewGuid();
        var expiredSession = new McpSession
        {
            SessionId = sessionId,
            UserObjectId = "test-user",
            CreatedUtc = DateTime.UtcNow.AddHours(-2),
            ExpiresUtc = DateTime.UtcNow.AddHours(-1) // Expired
        };
        manager.Seed(sessionId, expiredSession);

        var context = CreateContext("/mcp/tools");
        context.Request.Headers["X-MCP-Session"] = sessionId.ToString();

        await middleware.InvokeAsync(context, manager);

        Assert.True(calledNext);
        // Session should still be found (expiry handled elsewhere)
        Assert.True(context.User?.Identity?.IsAuthenticated == true);
        Assert.Equal("McpSession", context.User?.Identity?.AuthenticationType);
    }

    [Fact]
    public async Task HandlesInvalidSessionId_ContinuesExecution()
    {
        using var lf = LoggerFactory.Create(builder => { });
        var calledNext = false;
        var middleware = new McpSessionResolutionMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSessionResolutionMiddleware>());

        var manager = new FakeSessionManager();

        var context = CreateContext("/mcp/tools");
        context.Request.Headers["X-MCP-Session"] = "invalid-guid-format";

        await middleware.InvokeAsync(context, manager);

        Assert.True(calledNext);
        Assert.False(context.User?.Identity?.IsAuthenticated == true);
    }
}
