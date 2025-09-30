// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Greenlight.McpServer.Auth;
using Microsoft.Greenlight.McpServer.Middleware;
using Microsoft.Greenlight.McpServer.Options;
using Microsoft.Greenlight.McpServer.Services;
using Microsoft.Greenlight.McpServer.Tools;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management;
using Microsoft.Greenlight.Shared.Services.Security;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;

namespace Microsoft.Greenlight.McpServer.Servers;

/// <summary>
/// Business MCP Server with all tools for business task operations.
/// </summary>
public static class BusinessMcpServer
{
    /// <summary>
    /// Creates and configures the Business MCP server application.
    /// </summary>
    public static WebApplication CreateServer(string[] args, int port = 6008)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(BusinessMcpServer).Assembly.FullName,
            EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development
        });

        // Configure Kestrel for this specific server
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenLocalhost(port);
            serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
            serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
        });

        // Add service defaults
        builder.AddServiceDefaults();
        builder.Logging.AddConsole();

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

        // Configure MCP server with ALL tools from assembly
        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

        // Bind MCP options
        builder.Services.Configure<McpOptions>(
            builder.Configuration.GetSection("ServiceConfiguration:Mcp"));

        // Session management and services
        builder.Services.AddSingleton<IMcpSessionManager, McpSessionManager>();
        builder.Services.AddSingleton<IContentRelinkerService, ContentRelinkerService>();

        // Flow MCP Orleans streams integration
        builder.Services.AddSingleton<FlowMcpStreamSubscriptionService>();
        builder.Services.AddHostedService<FlowMcpStreamSubscriptionService>(provider =>
            provider.GetRequiredService<FlowMcpStreamSubscriptionService>());

        // Request timeouts
        builder.Services.AddRequestTimeouts();

        // Authentication setup
        ConfigureAuthentication(builder, credentialHelper);

        // Build the application
        var app = builder.Build();

        // Configure middleware pipeline
        ConfigureMiddleware(app, builder.Configuration);

        // Map MCP endpoints at /mcp path to match the YARP routing
        var mcpGroup = app.MapGroup("/mcp");
        var mcpEndpoint = mcpGroup.MapMcp()
            .WithRequestTimeout(TimeSpan.FromMinutes(10));

        // Apply authorization if enabled
        var authEnabled = !builder.Configuration.GetValue<bool>("ServiceConfiguration:Mcp:DisableAuth");
        if (authEnabled)
        {
            mcpEndpoint.RequireAuthorization();
        }

        app.Logger.LogInformation("Business MCP Server configured on port {Port}", port);

        return app;
    }

    private static void ConfigureAuthentication(WebApplicationBuilder builder, AzureCredentialHelper credentialHelper)
    {
        var disableAuth = builder.Configuration.GetValue<bool>("ServiceConfiguration:Mcp:DisableAuth");
        if (!disableAuth)
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
    }

    private static void ConfigureMiddleware(WebApplication app, IConfiguration configuration)
    {
        var disableAuth = configuration.GetValue<bool>("ServiceConfiguration:Mcp:DisableAuth");
        var authEnabled = !disableAuth;

        if (disableAuth)
        {
            app.Logger.LogWarning("Business MCP auth is DISABLED. Do NOT use this in production.");
        }

        if (authEnabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.UseMiddleware<McpSecretAuthenticationMiddleware>();
        app.UseRequestTimeouts();

        // Extract session ID from MCP request body and inject as header
        app.UseMiddleware<McpSessionInjectionMiddleware>();

        app.UseMiddleware<McpSessionResolutionMiddleware>();
    }
}