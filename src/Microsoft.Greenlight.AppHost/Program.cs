// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.AppHost;

internal static partial class Program
{
    private static void Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        // Configure container registry for publish mode
        ConfigureContainerRegistry(builder);

        // Setup configuration
        AppHostConfigurationSetup(builder);

        // Setup Kubernetes environment
        var kubernetes = SetupKubernetesEnvironment(builder);

        // Setup parameters and configuration
        var sqlPassword = builder.AddParameter("sqlPassword", secret: true);

        // Postgres (kmvectordb) administrator password parameter (local dev & provisioning)
        var postgresPassword = builder.AddParameter("postgresPassword", secret: true);

        var sqlDatabaseName = builder.Configuration["ServiceConfiguration:SQL:DatabaseName"]!;
        var usePostgres = builder.Configuration.GetValue<bool>("ServiceConfiguration:GreenlightServices:Global:UsePostgresMemory");

        // Setup Azure dependencies
        var azureDependencies = SetupAzureDependencies(builder, sqlPassword, postgresPassword, sqlDatabaseName, usePostgres);

        // Setup Orleans
        var orleans = SetupOrleans(builder, azureDependencies);

        // Setup all projects
        var projects = SetupProjects(builder, kubernetes, azureDependencies, orleans);

        // Apply feature toggles
        ApplyInsightsReferences(projects, azureDependencies.Insights);

        builder.Build().Run();
    }

    /// <summary>
    /// Configures the container registry for publishing container images
    /// </summary>
    /// <param name="builder">The distributed application builder</param>
    private static void ConfigureContainerRegistry(IDistributedApplicationBuilder builder)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            // Configure container registry via a non-secret parameter to let publishers consume it
            var acrName = builder.Configuration["AZURE_CONTAINER_REGISTRY_NAME"] ??
                          builder.Configuration["ACR_NAME"] ??
                          $"acr{builder.Configuration["AZURE_RESOURCE_GROUP"]?.Replace("-", "").Replace("rg", string.Empty)}";
            var computedEndpoint = string.IsNullOrWhiteSpace(acrName) ? string.Empty : $"{acrName}.azurecr.io";

            // Prefer parameter so publishers bind natively
            var containerRegistry = builder.AddParameter(
                name: "ContainerRegistry",
                value: computedEndpoint,
                publishValueAsDefault: true,
                secret: false);

            // Maintain existing configuration key for backward compatibility
            if (!string.IsNullOrWhiteSpace(computedEndpoint))
            {
                builder.Configuration["ContainerRegistry"] = computedEndpoint;
            }
        }
    }
}
