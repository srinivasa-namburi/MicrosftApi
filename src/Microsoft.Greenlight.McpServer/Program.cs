// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Greenlight.McpServer.Auth;
using Microsoft.Greenlight.McpServer.Endpoints;
using Microsoft.Greenlight.McpServer.Middleware;
using Microsoft.Greenlight.McpServer.Options;
using Microsoft.Greenlight.McpServer.Servers;
using Microsoft.Greenlight.McpServer.Services;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services.Security;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Configuration;

// ------------ Business MCP Server (port 6008) ------------
var businessMcpApp = BusinessMcpServer.CreateServer(args, port: 6008);

// ------------ Flow MCP Server (port 6007) ------------
var flowMcpApp = FlowMcpServer.CreateServer(args, port: 6007);

// ------------ YARP Proxy Frontend (port 6005 or configured) ------------
var proxyBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ApplicationName = typeof(Program).Assembly.FullName,
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development
});

// Configure Kestrel for the proxy
var proxyPort = proxyBuilder.Configuration.GetValue<int?>("Mcp:HttpPort") ?? 6005;
var existingUrls = proxyBuilder.Configuration["ASPNETCORE_URLS"];

if (string.IsNullOrWhiteSpace(existingUrls))
{
    proxyBuilder.WebHost.UseUrls($"http://localhost:{proxyPort}");
}
else
{
    // If URLs are already configured, add our HTTP port if not present
    var httpUrl = $"http://localhost:{proxyPort}";
    if (!existingUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(u => string.Equals(u, httpUrl, StringComparison.OrdinalIgnoreCase)))
    {
        proxyBuilder.WebHost.UseUrls($"{existingUrls};{httpUrl}");
    }
}

proxyBuilder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
});

// Add service defaults and minimal services for the proxy
proxyBuilder.AddServiceDefaults();
proxyBuilder.Logging.AddConsole();

// Setup credential helper and admin helper
var credentialHelper = new AzureCredentialHelper(proxyBuilder.Configuration);
proxyBuilder.Services.AddSingleton(credentialHelper);
AdminHelper.Initialize(proxyBuilder.Configuration);
AdminHelper.ValidateDeveloperSetup("MCP Server Proxy");

// Add HTTP context accessor for auth
proxyBuilder.Services.AddHttpContextAccessor();

// Add database context for session management
proxyBuilder.AddGreenlightDbContextAndConfiguration();

// Get service configuration for Greenlight services
var serviceConfigurationOptions = proxyBuilder.Configuration
    .GetSection(ServiceConfigurationOptions.PropertyName)
    .Get<ServiceConfigurationOptions>()!;

// Add core Greenlight services (includes IAppCache)
proxyBuilder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);

// Add Orleans client for grain access
proxyBuilder.AddGreenlightOrleansClient(credentialHelper);

// Register plugins and document processes (provides IKernelFactory, IAiEmbeddingService, etc.)
proxyBuilder.RegisterStaticPlugins(serviceConfigurationOptions);
proxyBuilder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

// Add hashing service for secret validation
proxyBuilder.Services.AddSingleton<ISecretHashingService, SecretHashingService>();

// Session management (shared across all endpoints)
proxyBuilder.Services.AddSingleton<IMcpSessionManager, McpSessionManager>();

// Content relinker service for proxy URLs
proxyBuilder.Services.AddSingleton<IContentRelinkerService, ContentRelinkerService>();

// Bind MCP options
proxyBuilder.Services.Configure<McpOptions>(
    proxyBuilder.Configuration.GetSection("ServiceConfiguration:Mcp"));

// Configure authentication for the proxy
var disableAuth = proxyBuilder.Configuration.GetValue<bool>("ServiceConfiguration:Mcp:DisableAuth");
var authEnabled = !disableAuth;

if (authEnabled)
{
    var azureAdSection = proxyBuilder.Configuration.GetSection("AzureAd");
    proxyBuilder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var instance = azureAdSection["Instance"] ?? "https://login.microsoftonline.com/";
            var tenantId = azureAdSection["TenantId"]!;
            var authority = $"{instance.TrimEnd('/')}/{tenantId}/v2.0";
            var audience = azureAdSection["Audience"] ?? azureAdSection["ClientId"];

            options.Authority = authority;
            options.Audience = audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidAudience = audience,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
            };
        });
    proxyBuilder.Services.AddAuthorization();
}

// Configure YARP reverse proxy
proxyBuilder.Services.AddReverseProxy()
    .LoadFromMemory(
        routes: new[]
        {
            new RouteConfig
            {
                RouteId = "flow-route",
                ClusterId = "flow-cluster",
                Match = new RouteMatch
                {
                    Path = "/flow/{**catch-all}"
                },
                Transforms = new[]
                {
                    new Dictionary<string, string> { ["PathRemovePrefix"] = "/flow" }
                }
            },
            new RouteConfig
            {
                RouteId = "mcp-route",
                ClusterId = "mcp-cluster",
                Match = new RouteMatch
                {
                    Path = "/mcp/{**catch-all}"
                }
            }
        },
        clusters: new[]
        {
            new ClusterConfig
            {
                ClusterId = "flow-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["flow1"] = new DestinationConfig { Address = "http://localhost:6007" }
                }
            },
            new ClusterConfig
            {
                ClusterId = "mcp-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["mcp1"] = new DestinationConfig { Address = "http://localhost:6008" }
                }
            }
        });

// Build the proxy application
var proxyApp = proxyBuilder.Build();

if (disableAuth)
{
    proxyApp.Logger.LogWarning("MCP auth is DISABLED via configuration (ServiceConfiguration:Mcp:DisableAuth). Do NOT use this in production.");
}

// Configure middleware pipeline for the proxy
if (authEnabled)
{
    proxyApp.UseAuthentication();
    proxyApp.UseAuthorization();
}

// Enable secret-based auth for MCP routes
proxyApp.UseMiddleware<McpSecretAuthenticationMiddleware>();

// Session resolution middleware for /mcp and /flow routes
proxyApp.UseWhen(ctx =>
    ctx.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase) ||
    ctx.Request.Path.StartsWithSegments("/flow", StringComparison.OrdinalIgnoreCase), branch =>
{
    branch.UseMiddleware<McpSessionResolutionMiddleware>();
});

// Map YARP proxy endpoints
proxyApp.MapReverseProxy();

// Map session management endpoints directly on the proxy (not proxied)
var sessionGroup = proxyApp.MapGroup("/mcp");
sessionGroup.MapMcpSessionEndpoints();

// OAuth metadata endpoint
if (authEnabled)
{
    var tenantId = proxyBuilder.Configuration.GetSection("AzureAd")["TenantId"]!;
    var authorityHost = new Uri(credentialHelper.DiscoveredAuthorityHost);
    var issuer = authorityHost.AbsoluteUri.TrimEnd('/') + $"/{tenantId}";

    proxyApp.MapGet("/.well-known/oauth-authorization-server", () => Results.Json(new
    {
        issuer,
        authorization_endpoint = $"{issuer}/oauth2/v2.0/authorize",
        token_endpoint = $"{issuer}/oauth2/v2.0/token",
        jwks_uri = $"{issuer}/discovery/v2.0/keys",
        response_types_supported = (string[])["code"],
        grant_types_supported = (string[])["authorization_code", "refresh_token"],
        code_challenge_methods_supported = (string[])["S256"]
    }));
}

// Back-compat convenience redirects
var rootRedirect = proxyApp.Map("/", (HttpContext ctx) =>
    Results.Redirect("/mcp", permanent: false, preserveMethod: true))
    .ExcludeFromDescription();
var sseRedirect = proxyApp.Map("/sse", (HttpContext ctx) =>
    Results.Redirect("/mcp/sse", permanent: false, preserveMethod: true))
    .ExcludeFromDescription();

if (authEnabled)
{
    rootRedirect.RequireAuthorization();
    sseRedirect.RequireAuthorization();
}

// ------------ Run all three servers in parallel ------------
using var cts = new CancellationTokenSource();

// Handle shutdown gracefully
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var runTasks = new[]
{
    businessMcpApp.RunAsync(cts.Token),
    flowMcpApp.RunAsync(cts.Token),
    proxyApp.RunAsync(cts.Token)
};

proxyApp.Logger.LogInformation("Starting MCP servers:");
proxyApp.Logger.LogInformation("  - Proxy (YARP) on port {ProxyPort}", proxyPort);
proxyApp.Logger.LogInformation("  - Business MCP on port 6008 (internal)");
proxyApp.Logger.LogInformation("  - Flow MCP on port 6007 (internal)");

try
{
    await Task.WhenAll(runTasks);
}
catch (TaskCanceledException)
{
    // Normal shutdown
    proxyApp.Logger.LogInformation("MCP servers shutting down...");
}
catch (Exception ex)
{
    proxyApp.Logger.LogError(ex, "Error running MCP servers");
    throw;
}