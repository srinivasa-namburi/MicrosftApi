// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.McpServer.Flow.Middleware;
using Microsoft.Greenlight.McpServer.Flow.Models;
using Microsoft.Greenlight.McpServer.Flow.Services;

namespace Microsoft.Greenlight.McpServer.Tests;

public class McpSessionResolutionMiddlewareTests
{
    private sealed class FakeSessionManager : IMcpSessionManager
    {
        private readonly Dictionary<string, McpSession> _sessions = new();

        public Task<ClaimsPrincipal?> ResolvePrincipalFromHeadersAsync(string serverNamespace, IHeaderDictionary headers, CancellationToken cancellationToken)
        {
            if (!headers.TryGetValue("X-Greenlight-Session", out var values))
            {
                return Task.FromResult<ClaimsPrincipal?>(null);
            }
            var raw = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Task.FromResult<ClaimsPrincipal?>(null);
            }
            var key = GetSessionKey(serverNamespace, raw);
            if (_sessions.TryGetValue(key, out var s))
            {
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, s.ProviderSubjectId),
                    new Claim("sub", s.ProviderSubjectId),
                    new Claim("oid", s.ProviderSubjectId)
                }, "McpSession");
                return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
            }
            return Task.FromResult<ClaimsPrincipal?>(null);
        }

        public Task<string?> GetSessionDataAsync(string serverNamespace, string mcpSessionId, string key, CancellationToken cancellationToken)
        {
            var sessionKey = GetSessionKey(serverNamespace, mcpSessionId);
            if (_sessions.TryGetValue(sessionKey, out var session) && session.SessionData.TryGetValue(key, out var value))
            {
                return Task.FromResult<string?>(value);
            }
            return Task.FromResult<string?>(null);
        }

        public Task SetSessionDataAsync(string serverNamespace, string mcpSessionId, string key, string value, CancellationToken cancellationToken)
        {
            var sessionKey = GetSessionKey(serverNamespace, mcpSessionId);
            if (_sessions.TryGetValue(sessionKey, out var session))
            {
                session.SessionData[key] = value;
            }
            return Task.CompletedTask;
        }

        public Task RemoveSessionDataAsync(string serverNamespace, string mcpSessionId, string key, CancellationToken cancellationToken)
        {
            var sessionKey = GetSessionKey(serverNamespace, mcpSessionId);
            if (_sessions.TryGetValue(sessionKey, out var session))
            {
                session.SessionData.Remove(key);
            }
            return Task.CompletedTask;
        }

        public Task<McpSession?> GetSessionAsync(string serverNamespace, string mcpSessionId, CancellationToken cancellationToken)
        {
            var sessionKey = GetSessionKey(serverNamespace, mcpSessionId);
            _sessions.TryGetValue(sessionKey, out var session);
            return Task.FromResult(session);
        }

        public Task<string?> GetProviderSubjectIdAsync(string serverNamespace, string mcpSessionId, CancellationToken cancellationToken)
        {
            return GetSessionDataAsync(serverNamespace, mcpSessionId, "_providerSubjectId", cancellationToken);
        }

        public Task SetProviderSubjectIdAsync(string serverNamespace, string mcpSessionId, string providerSubjectId, CancellationToken cancellationToken)
        {
            return SetSessionDataAsync(serverNamespace, mcpSessionId, "_providerSubjectId", providerSubjectId, cancellationToken);
        }

        public string? ResolveProviderSubjectId(ClaimsPrincipal user)
        {
            if (user == null)
            {
                return null;
            }
            return user.FindFirst("sub")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("oid")?.Value;
        }

        public void Seed(string serverNamespace, string id, McpSession value) => _sessions[GetSessionKey(serverNamespace, id)] = value;

        private static string GetSessionKey(string serverNamespace, string sessionId) => $"{serverNamespace}:{sessionId}";
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
        var sessionId = Guid.NewGuid().ToString();
        var session = new McpSession
        {
            SessionId = sessionId,
            ProviderSubjectId = "00000000-0000-0000-0000-000000000002",
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
        };

        var manager = new FakeSessionManager();
        manager.Seed("business", sessionId, session);

        using var lf = LoggerFactory.Create(builder => { });
        var calledNext = false;
        var middleware = new McpSessionResolutionMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSessionResolutionMiddleware>());

        var context = CreateContext("/mcp/tools");
        context.Request.Headers["X-Greenlight-Session"] = sessionId;

        var requestContext = new McpRequestContext();
        await middleware.InvokeAsync(context, manager, requestContext);

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
        var requestContext = new McpRequestContext();
        await middleware.InvokeAsync(context, manager, requestContext);

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
        context.Request.Headers["X-Greenlight-Session"] = Guid.NewGuid().ToString();

        var requestContext = new McpRequestContext();
        await middleware.InvokeAsync(context, manager, requestContext);

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
        var sessionId = Guid.NewGuid().ToString();
        var expiredSession = new McpSession
        {
            SessionId = sessionId,
            ProviderSubjectId = "test-user",
            CreatedUtc = DateTime.UtcNow.AddHours(-2),
            ExpiresUtc = DateTime.UtcNow.AddHours(-1) // Expired
        };
        manager.Seed("business", sessionId, expiredSession);

        var context = CreateContext("/mcp/tools");
        context.Request.Headers["X-Greenlight-Session"] = sessionId;

        var requestContext = new McpRequestContext();
        await middleware.InvokeAsync(context, manager, requestContext);

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
        context.Request.Headers["X-Greenlight-Session"] = "invalid-guid-format";

        var requestContext = new McpRequestContext();
        await middleware.InvokeAsync(context, manager, requestContext);

        Assert.True(calledNext);
        Assert.False(context.User?.Identity?.IsAuthenticated == true);
    }
}
