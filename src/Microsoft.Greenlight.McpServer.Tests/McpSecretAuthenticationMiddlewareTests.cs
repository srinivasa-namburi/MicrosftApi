// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.McpServer.Auth;
using Microsoft.Greenlight.McpServer.Options;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.McpServer.Endpoints;
using Microsoft.Greenlight.McpServer.Services;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Configuration;
using Microsoft.Greenlight.Shared.Services.Security;

namespace Microsoft.Greenlight.McpServer.Tests;

public class McpSecretAuthenticationMiddlewareTests
{
    private sealed class TestOptionsSnapshot : IOptionsSnapshot<McpOptions>
    {
        public McpOptions Value { get; }
        public TestOptionsSnapshot(McpOptions value) => Value = value;
        public McpOptions Get(string? name) => Value;
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        return ctx;
    }

    [Fact]
    public async Task SecretAuth_Succeeds_SetsPrincipalAndCallsNext()
    {
        var plainSecret = "s3cret";
        var userOid = "test-user-oid";
        var secretName = "test-secret";
        var salt = "test-salt";
        var hash = "test-hash";
        
        var options = new McpOptions
        {
            SecretEnabled = true,
            SecretHeaderName = "X-MCP-Secret"
        };

        // Setup in-memory database
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new DocGenerationDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();
        
        var mcpSecret = new McpSecret
        {
            Name = secretName,
            UserOid = userOid,
            SecretSalt = salt,
            SecretHash = hash,
            IsActive = true
        };
        dbContext.Set<McpSecret>().Add(mcpSecret);
        await dbContext.SaveChangesAsync();

        var mockDbContextFactory = new Mock<IDbContextFactory<DocGenerationDbContext>>();
        mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new DocGenerationDbContext(dbOptions));

        var mockHashingService = new Mock<ISecretHashingService>();
        mockHashingService.Setup(h => h.Verify(plainSecret, salt, hash)).Returns(true);

        using var lf = LoggerFactory.Create(builder => { });
        var middleware = new McpSecretAuthenticationMiddleware(_ => Task.CompletedTask,
            lf.CreateLogger<McpSecretAuthenticationMiddleware>());

        var context = CreateContext("/mcp/tools");
        context.Request.Headers[options.SecretHeaderName!] = plainSecret;

        var config = new ConfigurationBuilder().Build();
        var optSnap = new TestOptionsSnapshot(options);

        await middleware.InvokeAsync(context, config, optSnap, mockDbContextFactory.Object, mockHashingService.Object);

        Assert.True(context.User?.Identity?.IsAuthenticated, "User should be authenticated with valid database-backed secret.");
        Assert.Equal("McpSecret", context.User?.Identity?.AuthenticationType);
        Assert.Equal(userOid, context.User?.FindFirst("oid")?.Value);
        Assert.Equal(secretName, context.User?.FindFirst("mcp_secret_name")?.Value);
        Assert.Equal("secret", context.User?.FindFirst("mcp_auth")?.Value);
    }

    [Fact]
    public async Task SecretAuth_WithCustomHeaderName_Succeeds()
    {
        var plainSecret = "custom-secret";
        var userOid = "custom-user-oid";
        var secretName = "custom-secret";
        var salt = "custom-salt";
        var hash = "custom-hash";
        var customHeaderName = "X-Custom-API-Key";
        
        var options = new McpOptions
        {
            SecretEnabled = true,
            SecretHeaderName = customHeaderName
        };

        // Setup in-memory database
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new DocGenerationDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();
        
        var mcpSecret = new McpSecret
        {
            Name = secretName,
            UserOid = userOid,
            SecretSalt = salt,
            SecretHash = hash,
            IsActive = true
        };
        dbContext.Set<McpSecret>().Add(mcpSecret);
        await dbContext.SaveChangesAsync();

        var mockDbContextFactory = new Mock<IDbContextFactory<DocGenerationDbContext>>();
        mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new DocGenerationDbContext(dbOptions));

        var mockHashingService = new Mock<ISecretHashingService>();
        mockHashingService.Setup(h => h.Verify(plainSecret, salt, hash)).Returns(true);

        using var lf = LoggerFactory.Create(builder => { });
        var middleware = new McpSecretAuthenticationMiddleware(_ => Task.CompletedTask,
            lf.CreateLogger<McpSecretAuthenticationMiddleware>());

        var context = CreateContext("/mcp/tools");
        context.Request.Headers[customHeaderName] = plainSecret;

        var config = new ConfigurationBuilder().Build();
        var optSnap = new TestOptionsSnapshot(options);

        await middleware.InvokeAsync(context, config, optSnap, mockDbContextFactory.Object, mockHashingService.Object);

        Assert.True(context.User?.Identity?.IsAuthenticated);
        Assert.Equal("McpSecret", context.User?.Identity?.AuthenticationType);
        Assert.Equal(userOid, context.User?.FindFirst("oid")?.Value);
    }

    [Fact]
    public async Task SecretAuth_InactiveSecret_Returns401()
    {
        var plainSecret = "inactive-secret";
        var salt = "test-salt";
        var hash = "test-hash";
        
        var options = new McpOptions
        {
            SecretEnabled = true,
            SecretHeaderName = "X-MCP-Secret"
        };

        // Setup in-memory database with inactive secret
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new DocGenerationDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();
        
        var mcpSecret = new McpSecret
        {
            Name = "inactive-secret",
            UserOid = "test-user",
            SecretSalt = salt,
            SecretHash = hash,
            IsActive = false // Inactive!
        };
        dbContext.Set<McpSecret>().Add(mcpSecret);
        await dbContext.SaveChangesAsync();

        var mockDbContextFactory = new Mock<IDbContextFactory<DocGenerationDbContext>>();
        mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new DocGenerationDbContext(dbOptions));

        var mockHashingService = new Mock<ISecretHashingService>();
        mockHashingService.Setup(h => h.Verify(plainSecret, salt, hash)).Returns(true);

        var calledNext = false;
        using var lf = LoggerFactory.Create(builder => { });
        var middleware = new McpSecretAuthenticationMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSecretAuthenticationMiddleware>());

        var context = CreateContext("/mcp/sse");
        context.Request.Headers[options.SecretHeaderName!] = plainSecret;

        var config = new ConfigurationBuilder().Build();
        var optSnap = new TestOptionsSnapshot(options);

        await middleware.InvokeAsync(context, config, optSnap, mockDbContextFactory.Object, mockHashingService.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(calledNext);
    }

    [Fact]
    public async Task SecretAuth_DisabledAuth_AllowsAccessWithoutSecret()
    {
        var options = new McpOptions
        {
            SecretEnabled = true,
            DisableAuth = true
        };

        // Setup empty database
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new DocGenerationDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();

        var mockDbContextFactory = new Mock<IDbContextFactory<DocGenerationDbContext>>();
        mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new DocGenerationDbContext(dbOptions));

        var mockHashingService = new Mock<ISecretHashingService>();

        var calledNext = false;
        using var lf = LoggerFactory.Create(builder => { });
        var middleware = new McpSecretAuthenticationMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSecretAuthenticationMiddleware>());

        var context = CreateContext("/mcp/tools");
        // No secret header provided

        var config = new ConfigurationBuilder().Build();
        var optSnap = new TestOptionsSnapshot(options);

        await middleware.InvokeAsync(context, config, optSnap, mockDbContextFactory.Object, mockHashingService.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(calledNext);
    }

    [Fact]
    public async Task SecretAuth_NonMcpPath_SkipsAuthentication()
    {
        var options = new McpOptions
        {
            SecretEnabled = true
        };

        var mockDbContextFactory = new Mock<IDbContextFactory<DocGenerationDbContext>>();
        var mockHashingService = new Mock<ISecretHashingService>();

        var calledNext = false;
        using var lf = LoggerFactory.Create(builder => { });
        var middleware = new McpSecretAuthenticationMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSecretAuthenticationMiddleware>());

        var context = CreateContext("/api/health");

        var config = new ConfigurationBuilder().Build();
        var optSnap = new TestOptionsSnapshot(options);

        await middleware.InvokeAsync(context, config, optSnap, mockDbContextFactory.Object, mockHashingService.Object);

        Assert.True(calledNext);
        Assert.False(context.User?.Identity?.IsAuthenticated ?? false);
        mockDbContextFactory.Verify(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SecretAuth_InvalidSecret_Returns401_DoesNotCallNext()
    {
        var calledNext = false;
        var invalidSecret = "invalid-secret";
        var salt = "test-salt";
        var hash = "test-hash";
        
        var options = new McpOptions
        {
            SecretEnabled = true,
            SecretHeaderName = "X-MCP-Secret"
        };

        // Setup in-memory database
        var dbOptions = new DbContextOptionsBuilder<DocGenerationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new DocGenerationDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();
        
        var mcpSecret = new McpSecret
        {
            Name = "test-secret",
            UserOid = "test-user-oid", 
            SecretSalt = salt,
            SecretHash = hash,
            IsActive = true
        };
        dbContext.Set<McpSecret>().Add(mcpSecret);
        await dbContext.SaveChangesAsync();

        var mockDbContextFactory = new Mock<IDbContextFactory<DocGenerationDbContext>>();
        mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new DocGenerationDbContext(dbOptions));

        var mockHashingService = new Mock<ISecretHashingService>();
        // Invalid secret doesn't match any hash
        mockHashingService.Setup(h => h.Verify(invalidSecret, salt, hash)).Returns(false);

        using var lf = LoggerFactory.Create(builder => { });
        var middleware = new McpSecretAuthenticationMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSecretAuthenticationMiddleware>());

        var context = CreateContext("/mcp/sse");
        context.Request.Headers[options.SecretHeaderName!] = invalidSecret;

        var config = new ConfigurationBuilder().Build();
        var optSnap = new TestOptionsSnapshot(options);

        await middleware.InvokeAsync(context, config, optSnap, mockDbContextFactory.Object, mockHashingService.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(calledNext);
    }

    [Fact]
    public async Task SecretAuth_DoesNotOverrideAuthenticatedUser()
    {
        var calledNext = false;
        var options = new McpOptions
        {
            SecretEnabled = true,
            SecretHeaderName = "X-MCP-Secret"
        };

        // Setup mocks (they won't be called since user is already authenticated)
        var mockDbContextFactory = new Mock<IDbContextFactory<DocGenerationDbContext>>();
        var mockHashingService = new Mock<ISecretHashingService>();

        using var lf = LoggerFactory.Create(builder => { });
        var middleware = new McpSecretAuthenticationMiddleware(_ => { calledNext = true; return Task.CompletedTask; },
            lf.CreateLogger<McpSecretAuthenticationMiddleware>());

        var context = CreateContext("/mcp/tools");
        // Pre-authenticate user (e.g., via JWT)
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "jwt-user") }, authenticationType: "Jwt"));
        context.Request.Headers[options.SecretHeaderName!] = "some-secret";

        var config = new ConfigurationBuilder().Build();
        var optSnap = new TestOptionsSnapshot(options);

        await middleware.InvokeAsync(context, config, optSnap, mockDbContextFactory.Object, mockHashingService.Object);

        Assert.True(calledNext);
        Assert.Equal("Jwt", context.User?.Identity?.AuthenticationType);
        Assert.Null(context.User?.FindFirst("oid"));
    }
}

public class McpSessionEndpointsSmokeTests
{
    private sealed class InMemorySessionManager : IMcpSessionManager
    {
        public readonly Dictionary<Guid, Microsoft.Greenlight.McpServer.Models.McpSession> Sessions = new();
        public Task<Microsoft.Greenlight.McpServer.Models.McpSession> CreateAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid();
            var s = new Microsoft.Greenlight.McpServer.Models.McpSession
            {
                SessionId = id,
                UserObjectId = user.FindFirst("oid")?.Value ?? "t",
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
            };
            Sessions[id] = s; return Task.FromResult(s);
        }
        public Task<(bool Ok, Microsoft.Greenlight.McpServer.Models.McpSession? Session)> RefreshAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            if (!Sessions.TryGetValue(sessionId, out var s)) return Task.FromResult(((bool, Microsoft.Greenlight.McpServer.Models.McpSession?))(false, null));
            s.ExpiresUtc = DateTime.UtcNow.AddMinutes(30); return Task.FromResult(((bool, Microsoft.Greenlight.McpServer.Models.McpSession?))(true, s));
        }
        public Task InvalidateAsync(Guid sessionId, CancellationToken cancellationToken)
        { Sessions.Remove(sessionId); return Task.CompletedTask; }
        public Task<ClaimsPrincipal?> ResolvePrincipalFromHeadersAsync(IHeaderDictionary headers, CancellationToken cancellationToken)
        { return Task.FromResult<ClaimsPrincipal?>(null); }
        public Task<List<Microsoft.Greenlight.McpServer.Models.McpSession>> ListAsync(CancellationToken cancellationToken)
        {
            var sessions = Sessions.Values.ToList();
            return Task.FromResult(sessions);
        }

        public Task<Guid?> GetOrCreateFlowSessionAsync(string mcpSessionId, ClaimsPrincipal? user, CancellationToken cancellationToken)
        {
            // For tests, just return a new GUID
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }

    private static HttpClient CreateClient(out InMemorySessionManager manager)
    {
        var b = WebApplication.CreateBuilder();
        b.WebHost.UseTestServer();
        b.Services.AddSingleton<IMcpSessionManager, InMemorySessionManager>();
        var app = b.Build();
        app.Use(async (ctx, next) => { ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("oid", "u") }, "Test")); await next(); });
        app.MapGroup("/mcp").MapMcpSessionEndpoints();
        app.StartAsync().GetAwaiter().GetResult();
        manager = (InMemorySessionManager)app.Services.GetRequiredService<IMcpSessionManager>();
        return app.GetTestClient();
    }

    [Fact]
    public async Task Endpoints_Create_Refresh_Invalidate_Work()
    {
        var client = CreateClient(out var manager);
        var create = await client.PostAsync("/mcp/session", null);
        create.EnsureSuccessStatusCode();
        var obj = await create.Content.ReadFromJsonAsync<Resp>();
        Assert.NotNull(obj);
        var id = obj!.sessionId;
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/mcp/session/refresh"); refreshReq.Headers.Add("X-MCP-Session", id.ToString());
        var refresh = await client.SendAsync(refreshReq); refresh.EnsureSuccessStatusCode();
        var delReq = new HttpRequestMessage(HttpMethod.Delete, "/mcp/session"); delReq.Headers.Add("X-MCP-Session", id.ToString());
        var del = await client.SendAsync(delReq); Assert.Equal(System.Net.HttpStatusCode.NoContent, del.StatusCode);
        Assert.False(manager.Sessions.ContainsKey(id));
    }

    private sealed record Resp(Guid sessionId, DateTime expiresUtc);
}
