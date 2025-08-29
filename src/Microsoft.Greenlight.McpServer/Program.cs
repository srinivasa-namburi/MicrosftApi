// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Greenlight.Shared.Management;
using Microsoft.Greenlight.Shared.Management.Configuration;
using Microsoft.Greenlight.Shared.Notifiers;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.McpServer.Auth;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared; // ensure document process extension methods are available

var builder = WebApplication.CreateBuilder(args);


builder.AddServiceDefaults();

// Basic logging
builder.Logging.AddConsole();

// Load shared configuration and DB
var credentialHelper = new AzureCredentialHelper(builder.Configuration);
builder.Services.AddSingleton(credentialHelper);
// Initialize AdminHelper so shared services can determine environment correctly
AdminHelper.Initialize(builder.Configuration);

// Access HttpContext for tools to inspect user claims
builder.Services.AddHttpContextAccessor();

builder.AddGreenlightDbContextAndConfiguration();

// Add core Greenlight services and Orleans client to reach grains
var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName)
    .Get<ServiceConfigurationOptions>()!;
builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);
builder.AddGreenlightOrleansClient(credentialHelper);

// IMPORTANT: Register plugins and document process services (provides IKernelFactory, IAiEmbeddingService, etc.)
builder.RegisterStaticPlugins(serviceConfigurationOptions);
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

// Resolve API base address from Aspire-provided variables (services:api-main:https:0/http:0)
static Uri ResolveApiBaseUri(IConfiguration config, ServiceConfigurationOptions options)
{
    // Prefer https, fallback to http
    var apiAddress = config["services:api-main:https:0"] ?? config["services:api-main:http:0"]; 

    if (!string.IsNullOrWhiteSpace(apiAddress))
    {
        // Optionally override host while preserving port and scheme
        if (!string.IsNullOrWhiteSpace(options?.HostNameOverride?.Api))
        {
            var original = new Uri(apiAddress);
            var uriBuilder = new UriBuilder(original)
            {
                Host = options.HostNameOverride.Api
            };
            apiAddress = uriBuilder.Uri.ToString();
        }

        return new Uri(apiAddress.TrimEnd('/'));
    }

    // Fallback to service discovery scheme if explicit address not available
    return new Uri("https://api-main");
}

var apiBaseUri = ResolveApiBaseUri(builder.Configuration, serviceConfigurationOptions);

// Register HttpClient for API access
builder.Services.AddHttpClient("api-main", client =>
{
    client.BaseAddress = apiBaseUri;
});

// Provide AuthenticationStateProvider based on current HttpContext for server-side token access
builder.Services.AddScoped<AuthenticationStateProvider, HttpContextAuthenticationStateProvider>();

// Register shared DocumentGenerationApiClient (uses AuthenticationStateProvider to retrieve token)
builder.Services.AddTransient<IDocumentGenerationApiClient>(sp =>
{
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpFactory.CreateClient("api-main");
    var logger = sp.GetRequiredService<ILogger<DocumentGenerationApiClient>>();
    var authStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
    return new DocumentGenerationApiClient(httpClient, logger, authStateProvider);
});

// Subscribe to configuration updates via Orleans streams using client-side notifier
builder.Services.AddSingleton<IWorkStreamNotifier, ConfigurationWorkStreamNotifier>();
builder.Services.AddHostedService<OrleansStreamSubscriberService>();

// Minimal hosted services for local config refresh and graceful shutdown
builder.Services.AddHostedService<DatabaseConfigurationRefreshService>();
builder.Services.AddHostedService<ShutdownCleanupService>();

// Optional: JWT auth via JwtBearer (if AzureAd section exists)
var azureAdSection = builder.Configuration.GetSection("AzureAd");
// Allow temporarily disabling auth for local/manual testing via configuration
// Set either Mcp:DisableAuth=true or DisableAuth=true (e.g., env var Mcp__DisableAuth=true)
var disableAuth = builder.Configuration.GetValue<bool?>("Mcp:DisableAuth")
    ?? builder.Configuration.GetValue<bool?>("DisableAuth")
    ?? false;

var authEnabled = !disableAuth && azureAdSection.Exists() && !string.IsNullOrEmpty(azureAdSection["TenantId"]);
if (authEnabled)
{
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // Expected AzureAd config keys: Instance, TenantId, Audience (or ClientId)
            var instance = azureAdSection["Instance"] ?? "https://login.microsoftonline.com/";
            var tenantId = azureAdSection["TenantId"]!;
            var authority = $"{instance.TrimEnd('/')}/{tenantId}/v2.0";
            options.Authority = authority;

            // Accept tokens issued for this API (audience). Prefer Audience, fallback to ClientId
            var audience = azureAdSection["Audience"] ?? azureAdSection["ClientId"];
            if (!string.IsNullOrWhiteSpace(audience))
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidAudience = audience,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                };
            }
        });
    builder.Services.AddAuthorization();
}

// Dev-only: also expose HTTP endpoint (for MCP Inspector and tools which don't trust local dev certs)
try
{
    if (!AdminHelper.IsRunningInProduction())
    {
        var devHttpPort = builder.Configuration.GetValue<int?>("Mcp:HttpPort") ?? 6005;
        var existingUrls = builder.Configuration["ASPNETCORE_URLS"]; // may be set by launchSettings
        var httpUrl = $"http://localhost:{devHttpPort}";
        if (string.IsNullOrWhiteSpace(existingUrls))
        {
            builder.WebHost.UseUrls(httpUrl);
        }
        else if (!existingUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                               .Any(u => string.Equals(u, httpUrl, StringComparison.OrdinalIgnoreCase)))
        {
            builder.WebHost.UseUrls($"{existingUrls};{httpUrl}");
        }
    }
}
catch
{
    // Best-effort only; ignore if UseUrls cannot be applied
}

// MCP server over HTTP (SSE) with tools in current assembly
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

if (disableAuth)
{
    app.Logger.LogWarning("MCP auth is DISABLED via configuration (Mcp:DisableAuth/DisableAuth). Do NOT use this in production.");
}

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// Map MCP endpoints under "/mcp" to avoid root conflicts and align with common tooling expectations
var mcpGroup = app.MapGroup("/mcp");
var mcpEndpoint = mcpGroup.MapMcp();
if (authEnabled)
{
    mcpEndpoint.RequireAuthorization();

    var tenantId = azureAdSection["TenantId"]!;

    // Get Authority host from credential helper to handle sovereign clouds
    var authorityHost = new Uri(credentialHelper.DiscoveredAuthorityHost);
    var issuer = authorityHost.AbsoluteUri.TrimEnd('/') + $"/{tenantId}";

    // Expose standard OAuth 2.0 Authorization Server Metadata endpoint
    app.MapGet("/.well-known/oauth-authorization-server", () => Results.Json(new
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

// Back-compat convenience redirects: root to /mcp and /sse to /mcp/sse (preserve method e.g., POST -> 307)
var rootRedirect = app.Map("/", (HttpContext ctx) => Results.Redirect("/mcp", permanent: false, preserveMethod: true))
    .ExcludeFromDescription();
var sseRedirect = app.Map("/sse", (HttpContext ctx) => Results.Redirect("/mcp/sse", permanent: false, preserveMethod: true))
    .ExcludeFromDescription();
if (authEnabled)
{
    rootRedirect.RequireAuthorization();
    sseRedirect.RequireAuthorization();
}

app.Run();
