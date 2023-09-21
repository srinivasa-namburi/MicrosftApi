// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ProjectVico.Plugins.Shared.Extensions;

public static class ConfigExtensions
{

    public static IConfigurationBuilder BuildPluginConfigurationBuilder(this IConfigurationBuilder configBuilder)
    {
        string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        configBuilder.AddJsonFile(
            path: "appsettings.json",
            optional: true,
            reloadOnChange: true);

        configBuilder.AddJsonFile(
            path: $"appsettings.{environment}.json",
            optional: true,
            reloadOnChange: true);

        configBuilder.AddJsonFile(
            path: "local.settings.json",
            optional: true,
            reloadOnChange: true);

        // For settings from Azure App Configuration, see https://docs.microsoft.com/en-us/azure/azure-app-configuration/quickstart-aspnet-core-app?tabs=core5x
        string? appConfigConnectionString = configBuilder.Build()["Service:AppConfigService"];
        if (!string.IsNullOrWhiteSpace(appConfigConnectionString))
        {
            configBuilder.AddAzureAppConfiguration(
                options =>
                {
                    options.Connect(connectionString: appConfigConnectionString);
                    options.ConfigureRefresh(refresh =>
                    {
                        refresh.Register("Service:AppConfigService", refreshAll: true);
                    });
                });
        }

        configBuilder.AddEnvironmentVariables();

        configBuilder.AddUserSecrets(
            assembly: Assembly.GetExecutingAssembly(),
            optional: true,
            reloadOnChange: true);

        return configBuilder;
    }

    /// <summary>
    /// Build the configuration for the service.
    /// </summary>
    public static IHostBuilder AddPluginConfiguration(this IHostBuilder host)
    {
        string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        host.ConfigureAppConfiguration((builderContext, configBuilder) =>
        {
            configBuilder.BuildPluginConfigurationBuilder();
        });

        return host;
    }
}
