// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Reflection;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ProjectVico.Frontend.API.Extensions;

internal static class ConfigExtensions
{
    /// <summary>
    /// Build the configuration for the service.
    /// </summary>
    public static IHostBuilder AddConfiguration(this IHostBuilder host)
    {
        string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        host.ConfigureAppConfiguration((builderContext, configBuilder) =>
        {
            configBuilder.AddJsonFile(
                path: "appsettings.json",
                optional: false,
                reloadOnChange: true);

            configBuilder.AddJsonFile(
                path: $"appsettings.{environment}.json",
                optional: true,
                reloadOnChange: true);

            configBuilder.AddJsonFile(
                path: "appsettings.local.json",
                optional: true,
                reloadOnChange: true);

            configBuilder.AddEnvironmentVariables();

            configBuilder.AddUserSecrets(
                assembly: Assembly.GetExecutingAssembly(),
                optional: true,
                reloadOnChange: true);

            // For settings from Key Vault, see https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-8.0
            string? keyVaultUri = builderContext.Configuration["Service:KeyVault"];
            if (!string.IsNullOrWhiteSpace(keyVaultUri))
            {
                configBuilder.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    new DefaultAzureCredential());

                // for more information on how to use DefaultAzureCredential, see https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet
            }

            // For settings from Azure App Configuration, see https://docs.microsoft.com/en-us/azure/azure-app-configuration/quickstart-aspnet-core-app?tabs=core5x
            string? appConfigConnectionString = builderContext.Configuration["Service:AppConfigService"];
            if (!string.IsNullOrWhiteSpace(appConfigConnectionString))
            {
                configBuilder.AddAzureAppConfiguration(
                    options =>
                    {
                        options.Connect(connectionString:appConfigConnectionString);
                        options.ConfigureRefresh(refresh =>
                        {
                            refresh.Register("Service:AppConfigService", refreshAll: true);
                        });
                    });
            }
        });

        return host;
    }
}
