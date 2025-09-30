// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.McpServer.Endpoints;
using Microsoft.Greenlight.McpServer.Models;
using Microsoft.Greenlight.McpServer.Services;

namespace Microsoft.Greenlight.McpServer.Tests;

public class McpSessionEndpointsTests
{
    private sealed class InMemorySessionManager : IMcpSessionManager
    {
        public readonly Dictionary<Guid, McpSession> Sessions = new();

        public Task<McpSession> CreateAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid();
            var oid = user.FindFirst("oid")?.Value ?? "test-oid";
            var session = new McpSession
            {
                SessionId = id,
                UserObjectId = oid,
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
            };
            Sessions[id] = session;
            return Task.FromResult(session);
        }

        public Task InvalidateAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            Sessions.Remove(sessionId);
            return Task.CompletedTask;
        }

        public Task<List<McpSession>> ListAsync(CancellationToken cancellationToken)
        {
            var sessions = Sessions.Values.ToList();
            return Task.FromResult(sessions);
        }

        public Task<(bool Ok, McpSession? Session)> RefreshAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            if (!Sessions.TryGetValue(sessionId, out var s))
            {
                return Task.FromResult(((bool, McpSession?))(false, null));
            }
            s.ExpiresUtc = DateTime.UtcNow.AddMinutes(30);
            return Task.FromResult(((bool, McpSession?))(true, s));
        }

        public Task<ClaimsPrincipal?> ResolvePrincipalFromHeadersAsync(IHeaderDictionary headers, CancellationToken cancellationToken)
        {
            return Task.FromResult<ClaimsPrincipal?>(null);
        }

        public Task<Guid?> GetOrCreateFlowSessionAsync(string mcpSessionId, ClaimsPrincipal? user, CancellationToken cancellationToken)
        {
            // For tests, just return a new GUID
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }

    private static HttpClient CreateClient(out InMemorySessionManager manager)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        builder.Services.AddSingleton<IMcpSessionManager, InMemorySessionManager>();

        var app = builder.Build();

        // Pretend-authenticate requests
        app.Use(async (ctx, next) =>
        {
            var identity = new ClaimsIdentity(new[] { new Claim("oid", "00000000-0000-0000-0000-000000000099") }, "TestAuth");
            ctx.User = new ClaimsPrincipal(identity);
            await next();
        });

        var group = app.MapGroup("/mcp");
        group.MapMcpSessionEndpoints();

        app.StartAsync().GetAwaiter().GetResult();

        manager = (InMemorySessionManager)app.Services.GetRequiredService<IMcpSessionManager>();
        return app.GetTestClient();
    }

    [Fact]
    public async Task CreateRefreshInvalidate_Succeeds()
    {
        var client = CreateClient(out var manager);

        // Create
        var createResp = await client.PostAsync("/mcp/session", content: null);
        Assert.Equal(HttpStatusCode.OK, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<CreateResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.sessionId);

        // Refresh
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp/session/refresh");
        req.Headers.Add("X-MCP-Session", created.sessionId.ToString());
        var refreshResp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var refreshed = await refreshResp.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.Equal(created.sessionId, refreshed!.sessionId);

        // Invalidate
        var delReq = new HttpRequestMessage(HttpMethod.Delete, "/mcp/session");
        delReq.Headers.Add("X-MCP-Session", created.sessionId.ToString());
        var delResp = await client.SendAsync(delReq);
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        Assert.False(manager.Sessions.ContainsKey(created.sessionId));
    }

    private sealed record CreateResponse(Guid sessionId, DateTime expiresUtc);
    private sealed record RefreshResponse(Guid sessionId, DateTime expiresUtc);
}
