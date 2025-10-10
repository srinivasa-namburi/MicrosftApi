// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.McpServer.Flow.Auth;
using Microsoft.Greenlight.McpServer.Flow.Middleware;
using Microsoft.Greenlight.McpServer.Flow.Services;
using Microsoft.Greenlight.McpServer.Flow.Tools;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services.Security;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using Microsoft.IdentityModel.Tokens;

// Flow MCP Server - AI Assistant conversational tools
var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel
// Don't set this explicitly in Kestrel - it relies on launchSettings.json - only used for display purposes.
var port = builder.Configuration.GetValue<int?>("Mcp:HttpPort") ?? 6007;

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
});

// Add service defaults
builder.AddServiceDefaults();
builder.Logging.AddConsole();

// Enable scopes for log prefix
builder.Services.Configure<Microsoft.Extensions.Logging.Console.ConsoleFormatterOptions>(options =>
{
    options.IncludeScopes = true;
});

// Setup core services
var credentialHelper = new AzureCredentialHelper(builder.Configuration);
builder.Services.AddSingleton(credentialHelper);
AdminHelper.Initialize(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.AddGreenlightDbContextAndConfiguration();
builder.Services.AddSingleton<ISecretHashingService, SecretHashingService>();

// Get service configuration
var serviceConfigurationOptions = builder.Configuration
    .GetSection(ServiceConfigurationOptions.PropertyName)
    .Get<ServiceConfigurationOptions>()!;

// Add Greenlight services and Orleans
builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);
builder.AddGreenlightOrleansClient(credentialHelper);

// Register plugins and document processes
builder.RegisterStaticPlugins(serviceConfigurationOptions);
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

// Register AuthenticationStateProvider for service clients
builder.Services.AddScoped<AuthenticationStateProvider, HttpContextAuthenticationStateProvider>();

// Register HTTP clients for API calls
var apiUri = new Uri(AdminHelper.GetApiServiceUrl());
builder.Services.AddHttpClient<IDocumentGenerationApiClient, DocumentGenerationApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});

// Configure MCP server with Flow tools only
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools([typeof(FlowTools), typeof(StreamingFlowTools)]);

// Bind MCP options
builder.Services.Configure<ServiceConfigurationOptions.McpOptions>(
    builder.Configuration.GetSection("ServiceConfiguration:Mcp"));

// Session management and services
builder.Services.AddSingleton<IMcpSessionManager, McpSessionManager>();
builder.Services.AddScoped<McpRequestContext>();
builder.Services.AddSingleton<IContentRelinkerService, ContentRelinkerService>();

// Flow MCP Orleans streams integration
builder.Services.AddSingleton<FlowMcpStreamSubscriptionService>();
builder.Services.AddHostedService<FlowMcpStreamSubscriptionService>(provider =>
    provider.GetRequiredService<FlowMcpStreamSubscriptionService>());

// Request timeouts
builder.Services.AddRequestTimeouts();

// Authentication setup (JWT)
ConfigureAuthentication(builder, credentialHelper);

// Add hosted services for configuration refresh
builder.Services.AddGreenlightHostedServices(addSignalrNotifiers: false);

// Build the application
var app = builder.Build();

// Set the configured namespace for this MCP server instance
McpRequestContext.ConfiguredServerNamespace = "flow";

// Configure middleware pipeline
ConfigureMiddleware(app, builder.Configuration);

// Map MCP endpoints at /mcp
app.MapMcp("/mcp")
    .WithRequestTimeout(TimeSpan.FromMinutes(10));

app.Logger.LogInformation("[FlowMcp] Flow MCP Server configured on port {Port}", port);

app.Run();

static void ConfigureAuthentication(WebApplicationBuilder builder, AzureCredentialHelper credentialHelper)
{
    var azureAdSection = builder.Configuration.GetSection("AzureAd");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
    builder.Services.AddAuthorization();
}

static void ConfigureMiddleware(WebApplication app, IConfiguration configuration)
{
    // Add log scope and diagnostic logging for all requests
    app.Use(async (context, next) =>
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FlowMcp");
        using (logger.BeginScope("[FlowMcp]"))
        {
            // Log incoming request details
            logger.LogInformation("Incoming request: Method={Method}, Path={Path}, ContentType={ContentType}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.ContentType ?? "none");

            await next(context);

            // Log response status
            logger.LogInformation("Response: StatusCode={StatusCode}, Path={Path}",
                context.Response.StatusCode,
                context.Request.Path.Value);
        }
    });

    // Ensure routing is properly configured
    app.UseRouting();

    // Authentication and authorization
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseRequestTimeouts();

    // Resolve user principal from session headers
    app.UseMiddleware<McpSessionResolutionMiddleware>();
}
