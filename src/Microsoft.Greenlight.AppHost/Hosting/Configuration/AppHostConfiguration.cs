// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.AppHost;

/// <summary>
/// AppHost configuration setup for Development/Production environments
/// </summary>
internal static partial class Program
{
    /// <summary>
    /// Sets up environment-specific configuration sources
    /// </summary>
    /// <param name="distributedApplicationBuilder">The distributed application builder</param>
    internal static void AppHostConfigurationSetup(IDistributedApplicationBuilder distributedApplicationBuilder)
    {
        distributedApplicationBuilder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        if (distributedApplicationBuilder.ExecutionContext.IsRunMode)
        {
            distributedApplicationBuilder
                .Configuration
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
            distributedApplicationBuilder.Configuration.AddUserSecrets(typeof(Program).Assembly);
        }
        else
        {
            distributedApplicationBuilder
                .Configuration
                .AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true);
        }

        distributedApplicationBuilder.Configuration.AddEnvironmentVariables();

        // Initialize AdminHelper
        AdminHelper.Initialize(distributedApplicationBuilder.Configuration);

        // Validate developer setup before proceeding
        // Note: Aspire automatically sets IsRunMode=false during 'aspire publish'
        // but we check it explicitly to be safe
        if (distributedApplicationBuilder.ExecutionContext.IsRunMode)
        {
            AdminHelper.ValidateDeveloperSetup("AppHost");
        }
        
        // Configure logging to ensure proper visibility
        ConfigureLogging(distributedApplicationBuilder);
        
        // Custom domain configuration support
        ConfigureHostnameOverride(distributedApplicationBuilder);
    }
    
    /// <summary>
    /// Configures hostname override settings from environment variables
    /// </summary>
    /// <param name="builder">The distributed application builder</param>
    private static void ConfigureHostnameOverride(IDistributedApplicationBuilder builder)
    {
        var hostnameOverride = builder.Configuration["HOSTNAME_OVERRIDE"];
        if (string.IsNullOrEmpty(hostnameOverride))
            return;
            
        try
        {
            var config = JsonSerializer.Deserialize<HostnameOverrideConfig>(hostnameOverride);
            if (config != null)
            {
                // Map to ServiceConfiguration section that applications expect
                if (!string.IsNullOrEmpty(config.WebApplicationUrl))
                {
                    builder.Configuration["ServiceConfiguration:HostNameOverride:WebApplicationUrl"] = config.WebApplicationUrl;
                }
                
                if (!string.IsNullOrEmpty(config.ApiBaseUrl))
                {
                    builder.Configuration["ServiceConfiguration:HostNameOverride:ApiBaseUrl"] = config.ApiBaseUrl;
                    // SignalR hosted on API should use the same base URL as the API
                    builder.Configuration["ServiceConfiguration:HostNameOverride:SignalRBaseUrl"] = config.ApiBaseUrl;
                }
                
                // Support Application Gateway unified endpoint (single domain for all services)
                if (!string.IsNullOrEmpty(config.ApplicationGatewayUrl))
                {
                    builder.Configuration["ServiceConfiguration:HostNameOverride:ApplicationGatewayUrl"] = config.ApplicationGatewayUrl;
                    // When using unified Application Gateway, all services share the same base URL
                    builder.Configuration["ServiceConfiguration:HostNameOverride:WebApplicationUrl"] = config.ApplicationGatewayUrl;
                    builder.Configuration["ServiceConfiguration:HostNameOverride:ApiBaseUrl"] = config.ApplicationGatewayUrl;
                    builder.Configuration["ServiceConfiguration:HostNameOverride:SignalRBaseUrl"] = config.ApplicationGatewayUrl;
                    builder.Configuration["ServiceConfiguration:HostNameOverride:McpBaseUrl"] = config.ApplicationGatewayUrl;
                }
            }
        }
        catch (JsonException ex)
        {
            // Log the error but don't fail the application startup
            Console.WriteLine($"Warning: Invalid HOSTNAME_OVERRIDE JSON format: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Configures logging to ensure AppHost output is properly visible
    /// </summary>
    /// <param name="builder">The distributed application builder</param>
    private static void ConfigureLogging(IDistributedApplicationBuilder builder)
    {
        // In Aspire, logging configuration is primarily handled through appsettings.json
        // We'll just ensure that console output isn't suppressed and add informational messages
        Console.WriteLine("[AppHost] Logging configuration loaded from appsettings.json");
        
        if (builder.Environment.EnvironmentName == "Development")
        {
            Console.WriteLine("[AppHost] Development mode detected - verbose logging enabled");
        }
    }
    
    /// <summary>
    /// Configuration model for hostname override settings
    /// </summary>
    /// <param name="WebApplicationUrl">Custom URL for web application</param>
    /// <param name="ApiBaseUrl">Custom URL for API endpoints (also used for SignalR when self-hosted)</param>
    /// <param name="ApplicationGatewayUrl">Unified URL for all services via Application Gateway</param>
    internal record HostnameOverrideConfig(
        string? WebApplicationUrl,
        string? ApiBaseUrl,
        string? ApplicationGatewayUrl
    );
}