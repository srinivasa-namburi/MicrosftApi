// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Net.Sockets;
using System.Text.Json;
using Aspire.Hosting.Kubernetes;
using Aspire.Hosting.Kubernetes.Resources;
using k8s.Models;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.AppHost;

/// <summary>
/// Project services configuration with references and dependencies
/// </summary>
internal static partial class Program
{
    /// <summary>
    /// Project resources result
    /// </summary>
    internal readonly struct ProjectResources
    {
        internal readonly IResourceBuilder<ProjectResource> DbSetupManager;
        internal readonly IResourceBuilder<ProjectResource> ApiMain;
        internal readonly IResourceBuilder<ProjectResource> McpServer;
        internal readonly IResourceBuilder<ProjectResource> Silo;
        internal readonly IResourceBuilder<ProjectResource> DocGenFrontend;

        internal ProjectResources(
            IResourceBuilder<ProjectResource> dbSetupManager,
            IResourceBuilder<ProjectResource> apiMain,
            IResourceBuilder<ProjectResource> mcpServer,
            IResourceBuilder<ProjectResource> silo,
            IResourceBuilder<ProjectResource> docGenFrontend)
        {
            DbSetupManager = dbSetupManager;
            ApiMain = apiMain;
            McpServer = mcpServer;
            Silo = silo;
            DocGenFrontend = docGenFrontend;
        }
    }

    /// <summary>
    /// Sets up all project services with proper references and wait dependencies
    /// </summary>
    /// <param name="builder">The distributed application builder</param>
    /// <param name="k8s">Kubernetes environment</param>
    /// <param name="azureDependencies">Azure dependencies</param>
    /// <param name="orleans">Orleans resources</param>
    /// <returns>Configured project resources</returns>
    internal static ProjectResources SetupProjects(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<KubernetesEnvironmentResource> k8s,
        AzureDependencies azureDependencies,
        OrleansResources orleans)
    {
        var envServiceConfigurationConfigurationSection = builder.Configuration.GetSection("ServiceConfiguration");
        var envAzureAdConfigurationSection = builder.Configuration.GetSection("AzureAd");
        var envConnectionStringsConfigurationSection = builder.Configuration.GetSection("ConnectionStrings");
        var envAzureConfigurationSection = builder.Configuration.GetSection("Azure");
        var envKestrelConfigurationSection = builder.Configuration.GetSection("Kestrel");
        var envMcpConfigurationSection = builder.Configuration.GetSection("Mcp");
        
        // Parse Kubernetes resource configurations if provided
        var resourceConfigs = ParseKubernetesResourceConfigs(builder.Configuration);

        // Setup Database Manager
        var dbSetupManagerBuilder = builder
            .AddProject<Projects.Microsoft_Greenlight_SetupManager_DB>("db-setupmanager")
            .WithComputeEnvironment(k8s)
            .WithDotNetContainerDefaults()
            .WithReplicas(1) // There can only be one Setup Manager
            .WithConfigSection(envServiceConfigurationConfigurationSection)
            .WithConfigSection(envConnectionStringsConfigurationSection)
            .WithConfigSection(envAzureConfigurationSection)
            .WithConfigSection(envAzureAdConfigurationSection)
            .WithReference(azureDependencies.DocGenSql)
            .WithReference(azureDependencies.RedisResource)
            .WaitFor(azureDependencies.DocGenSql) // We need this to be up and running before we can run the setup manager
            .WaitFor(azureDependencies.RedisResource); // Wait for this to make it ready for other services

        var usePostgres = builder.Configuration.GetValue<bool>("ServiceConfiguration:GreenlightServices:Global:UsePostgresMemory");
        if (usePostgres && azureDependencies.KmVectorDb != null)
        {
            dbSetupManagerBuilder.WithReference(azureDependencies.KmVectorDb);
        }
        var dbSetupManager = dbSetupManagerBuilder;

        // Setup API Main
        // In publish mode, exclude launch profile and manually specify HTTP endpoint to avoid duplicate ports
        var apiMainBuilder = builder.ExecutionContext.IsPublishMode
            ? builder.AddProject<Projects.Microsoft_Greenlight_API_Main>("api-main", launchProfileName: null)
                .WithHttpEndpoint(port: 8080, name: "http")
            : builder.AddProject<Projects.Microsoft_Greenlight_API_Main>("api-main")
                .WithExternalHttpEndpoints();

        apiMainBuilder
            .WithComputeEnvironment(k8s)
            .WithDotNetContainerDefaults()
            .WithConfigSection(envAzureAdConfigurationSection)
            .WithConfigSection(envServiceConfigurationConfigurationSection)
            .WithConfigSection(envConnectionStringsConfigurationSection)
            .WithConfigSection(envAzureConfigurationSection)
            .WithConfigSection(envKestrelConfigurationSection)
            .WithReference(azureDependencies.BlobStorage)
            .WithReference(azureDependencies.RedisResource)
            .WithReference(azureDependencies.DocGenSql)
            .WithReference(orleans.Orleans.AsClient())
            .WithReference(orleans.Checkpointing)
            .WithReference(orleans.BlobStorage)
            .WithReference(orleans.ClusteringTable)
            .WaitForCompletion(dbSetupManager);

        // Add EventHub reference only if it exists (production or configured locally)
        if (azureDependencies.EventHub != null)
        {
            apiMainBuilder.WithReference(azureDependencies.EventHub);
        }
        
        // Add Azure AI Search reference only if it exists (when not using Postgres)
        if (azureDependencies.AzureAiSearch != null)
        {
            apiMainBuilder.WithReference(azureDependencies.AzureAiSearch);
        }
        
        if (usePostgres && azureDependencies.KmVectorDb != null)
        {
            apiMainBuilder.WithReference(azureDependencies.KmVectorDb);
        }

        if (azureDependencies.SignalR != null)
        {
            apiMainBuilder.WithReference(azureDependencies.SignalR);
        }

        // Add reference to SignalR Redis backplane
        if (azureDependencies.RedisSignalR != null)
        {
            apiMainBuilder.WithReference(azureDependencies.RedisSignalR);
        }

        // In production, exclude HTTPS endpoints (containers behind ingress don't need HTTPS)
        if (builder.ExecutionContext.IsPublishMode)
        {
            apiMainBuilder.WithEndpointsInEnvironment(static endpoint =>
            {
                // Only include HTTP endpoints in production
                return endpoint.UriScheme != "https";
            });
        }

        // In publish mode, ensure Kubernetes Service names are stable (no "-service" suffix)
        if (builder.ExecutionContext.IsPublishMode)
        {
            apiMainBuilder.PublishAsKubernetesService(res =>
            {
                if (res.Service is null)
                {
                    return;
                }

                res.Service.Metadata.Name = "api-main";
                res.Service.Spec ??= new ServiceSpecV1();
                res.Service.Spec.Type = "ClusterIP";

                foreach (var port in res.Service.Spec.Ports)
                {
                    switch (port.Name)
                    {
                        case "http":
                            port.Port = 8080;
                            port.TargetPort = 8080;
                            break;
                        case "orleans-silo":
                            port.Port = 11111;
                            port.TargetPort = 11111;
                            break;
                        case "orleans-gateway":
                            port.Port = 30000;
                            port.TargetPort = 30000;
                            break;
                    }
                    port.Protocol = string.IsNullOrEmpty(port.Protocol) ? "TCP" : port.Protocol;
                }
            });
        }

        var apiMain = apiMainBuilder;

        // Configure Orleans endpoints for api-main (it's also an Orleans silo)
        if (builder.ExecutionContext.IsRunMode)
        {
            // Pin Orleans ports for development mode to prevent port shuffling
            Console.WriteLine("[AppHost] Setting fixed Orleans ports for api-main development: Gateway=10092, Silo=10093");
            apiMain.WithEnvironment("Orleans__Endpoints__GatewayPort", "10092")
                   .WithEnvironment("Orleans__Endpoints__SiloPort", "10093");
        }
        else if (builder.ExecutionContext.IsPublishMode)
        {
            // For Kubernetes/publish mode, configure Orleans to use standard ports
            // api-main needs same Orleans ports as silo for proper cluster participation
            Console.WriteLine("[AppHost] Setting Orleans ports for api-main Kubernetes: Gateway=30000, Silo=11111");

            // Configure api-main with explicit Orleans endpoints for Kubernetes
            apiMain.WithEndpoint("orleans-silo", endpoint =>
            {
                endpoint.Port = 11111;
                endpoint.TargetPort = 11111;
                endpoint.Protocol = ProtocolType.Tcp;
                endpoint.Transport = "tcp";
                endpoint.IsExternal = false; // Silo-to-silo communication is internal pod-to-pod
            })
            .WithEndpoint("orleans-gateway", endpoint =>
            {
                endpoint.Port = 30000;
                endpoint.TargetPort = 30000;
                endpoint.Protocol = ProtocolType.Tcp;
                endpoint.Transport = "tcp";
                endpoint.IsExternal = true; // Gateway accepts client connections
            })
            .WithEnvironment("Orleans__Endpoints__SiloPort", "11111")
            .WithEnvironment("Orleans__Endpoints__GatewayPort", "30000");
        }

        // MCP Core Server - Business document processing tools via Model Context Protocol over HTTP
        var mcpServerCoreBuilder = builder.ExecutionContext.IsPublishMode
            ? builder.AddProject<Projects.Microsoft_Greenlight_McpServer_Core>("mcpserver-core", launchProfileName: null)
                .WithHttpEndpoint(port: 8080, name: "http")
            : builder.AddProject<Projects.Microsoft_Greenlight_McpServer_Core>("mcpserver-core")
                .WithExternalHttpEndpoints();

        mcpServerCoreBuilder
            .WithComputeEnvironment(k8s)
            .WithDotNetContainerDefaults()
            .WithConfigSection(envMcpConfigurationSection)
            .WithConfigSection(envAzureAdConfigurationSection)
            .WithConfigSection(envServiceConfigurationConfigurationSection)
            .WithConfigSection(envConnectionStringsConfigurationSection)
            .WithConfigSection(envAzureConfigurationSection)
            .WithConfigSection(envKestrelConfigurationSection)
            .WithReference(apiMain)
            .WithReference(azureDependencies.BlobStorage)
            .WithReference(azureDependencies.RedisResource)
            .WithReference(azureDependencies.DocGenSql)
            .WithReference(orleans.Orleans.AsClient())
            .WithReference(orleans.Checkpointing)
            .WithReference(orleans.BlobStorage)
            .WithReference(orleans.ClusteringTable)
            .WaitForCompletion(dbSetupManager);

        // Add EventHub reference to Core server if it exists
        if (azureDependencies.EventHub != null)
        {
            mcpServerCoreBuilder.WithReference(azureDependencies.EventHub);
        }

        // Add Azure AI Search reference to Core server if it exists
        if (azureDependencies.AzureAiSearch != null)
        {
            mcpServerCoreBuilder.WithReference(azureDependencies.AzureAiSearch);
        }

        if (usePostgres && azureDependencies.KmVectorDb != null)
        {
            mcpServerCoreBuilder.WithReference(azureDependencies.KmVectorDb);
        }
        if (azureDependencies.SignalR != null)
        {
            mcpServerCoreBuilder.WithReference(azureDependencies.SignalR);
        }

        if (builder.ExecutionContext.IsRunMode)
        {
            mcpServerCoreBuilder.WithEnvironment("DOTNET_LAUNCH_PROFILE", "https");
        }

        // Stable Kubernetes Service name for Core MCP in publish mode
        if (builder.ExecutionContext.IsPublishMode)
        {
            mcpServerCoreBuilder.PublishAsKubernetesService(res =>
            {
                if (res.Service is null)
                {
                    return;
                }

                res.Service.Metadata.Name = "mcpserver-core";
                res.Service.Spec ??= new ServiceSpecV1();
                res.Service.Spec.Type = "ClusterIP";

                foreach (var port in res.Service.Spec.Ports)
                {
                    if (port.Name == "http")
                    {
                        port.Port = 8080;
                        port.TargetPort = 8080;
                        port.Protocol = string.IsNullOrEmpty(port.Protocol) ? "TCP" : port.Protocol;
                    }
                }
            });
        }

        // MCP Flow Server - AI Assistant conversational tools via Model Context Protocol
        var mcpServerFlowBuilder = builder.ExecutionContext.IsPublishMode
            ? builder.AddProject<Projects.Microsoft_Greenlight_McpServer_Flow>("mcpserver-flow", launchProfileName: null)
                .WithHttpEndpoint(port: 8080, name: "http")
            : builder.AddProject<Projects.Microsoft_Greenlight_McpServer_Flow>("mcpserver-flow")
                .WithExternalHttpEndpoints();

        mcpServerFlowBuilder
            .WithComputeEnvironment(k8s)
            .WithDotNetContainerDefaults()
            .WithConfigSection(envMcpConfigurationSection)
            .WithConfigSection(envAzureAdConfigurationSection)
            .WithConfigSection(envServiceConfigurationConfigurationSection)
            .WithConfigSection(envConnectionStringsConfigurationSection)
            .WithConfigSection(envAzureConfigurationSection)
            .WithConfigSection(envKestrelConfigurationSection)
            .WithReference(apiMain)
            .WithReference(azureDependencies.BlobStorage)
            .WithReference(azureDependencies.RedisResource)
            .WithReference(azureDependencies.DocGenSql)
            .WithReference(orleans.Orleans.AsClient())
            .WithReference(orleans.Checkpointing)
            .WithReference(orleans.BlobStorage)
            .WithReference(orleans.ClusteringTable)
            .WaitForCompletion(dbSetupManager);

        // Add EventHub reference to Flow server if it exists
        if (azureDependencies.EventHub != null)
        {
            mcpServerFlowBuilder.WithReference(azureDependencies.EventHub);
        }

        // Add Azure AI Search reference to Flow server if it exists
        if (azureDependencies.AzureAiSearch != null)
        {
            mcpServerFlowBuilder.WithReference(azureDependencies.AzureAiSearch);
        }

        if (usePostgres && azureDependencies.KmVectorDb != null)
        {
            mcpServerFlowBuilder.WithReference(azureDependencies.KmVectorDb);
        }
        if (azureDependencies.SignalR != null)
        {
            mcpServerFlowBuilder.WithReference(azureDependencies.SignalR);
        }

        if (builder.ExecutionContext.IsRunMode)
        {
            mcpServerFlowBuilder.WithEnvironment("DOTNET_LAUNCH_PROFILE", "https");
        }

        // Stable Kubernetes Service name for Flow MCP in publish mode
        if (builder.ExecutionContext.IsPublishMode)
        {
            mcpServerFlowBuilder.PublishAsKubernetesService(res =>
            {
                if (res.Service is null)
                {
                    return;
                }

                res.Service.Metadata.Name = "mcpserver-flow";
                res.Service.Spec ??= new ServiceSpecV1();
                res.Service.Spec.Type = "ClusterIP";

                foreach (var port in res.Service.Spec.Ports)
                {
                    if (port.Name == "http")
                    {
                        port.Port = 8080;
                        port.TargetPort = 8080;
                        port.Protocol = string.IsNullOrEmpty(port.Protocol) ? "TCP" : port.Protocol;
                    }
                }
            });
        }

        var mcpServer = mcpServerCoreBuilder;  // Legacy variable for backward compat
        var mcpServerCore = mcpServerCoreBuilder;
        var mcpServerFlow = mcpServerFlowBuilder;

        // Setup Silo
        var siloBuilder = builder.AddProject<Projects.Microsoft_Greenlight_Silo>("silo")
            .WithReplicas(1)
            .WithComputeEnvironment(k8s)
            .WithDotNetContainerDefaults()
            .WithConfigSection(envServiceConfigurationConfigurationSection)
            .WithConfigSection(envConnectionStringsConfigurationSection)
            .WithConfigSection(envAzureConfigurationSection)
            .WithConfigSection(envAzureAdConfigurationSection)
            .WithReference(azureDependencies.BlobStorage)
            .WithReference(azureDependencies.DocGenSql)
            .WithReference(azureDependencies.RedisResource)
            .WithReference(orleans.Orleans)
            .WithReference(orleans.Checkpointing)
            .WithReference(orleans.BlobStorage)
            .WithReference(orleans.ClusteringTable)
            .WithReference(apiMain)
            .WaitForCompletion(dbSetupManager);

        // Add EventHub reference only if it exists (production or configured locally)
        if (azureDependencies.EventHub != null)
        {
            siloBuilder.WithReference(azureDependencies.EventHub);
        }
        
        // Add Azure AI Search reference only if it exists (when not using Postgres)
        if (azureDependencies.AzureAiSearch != null)
        {
            siloBuilder.WithReference(azureDependencies.AzureAiSearch);
        }
        
        if (usePostgres && azureDependencies.KmVectorDb != null)
        {
            siloBuilder.WithReference(azureDependencies.KmVectorDb);
        }
        var silo = siloBuilder;

        if (builder.ExecutionContext.IsRunMode)
        {
            // Pin Orleans silo to fixed ports in development mode to prevent
            // port shuffling that can delay startup. This is intentional and not an error.
            // Orleans may log warnings about port configuration, but this is expected behavior.
            Console.WriteLine("[AppHost] Setting fixed Orleans ports for development: Gateway=10090, Silo=10091");
            silo.WithEnvironment("Orleans__Endpoints__GatewayPort", "10090")
                .WithEnvironment("Orleans__Endpoints__SiloPort", "10091");
        }
        else if (builder.ExecutionContext.IsPublishMode)
        {
            // For Kubernetes/publish mode, configure Orleans to use standard ports
            // Orleans uses direct pod-to-pod communication for clustering
            Console.WriteLine("[AppHost] Setting Orleans ports for Kubernetes: Gateway=30000, Silo=11111");

            // Configure the silo with explicit endpoints for Kubernetes
            // NOTE: As of Aspire 9.4.2, WithEndpoint doesn't properly set ports for Kubernetes publish,
            // but we set them here for future compatibility when Aspire fixes this issue
            silo.WithEndpoint("orleans-silo", endpoint =>
            {
                endpoint.Port = 11111;
                endpoint.TargetPort = 11111;
                endpoint.Protocol = ProtocolType.Tcp;
                endpoint.Transport = "tcp";
                endpoint.IsExternal = false; // Silo-to-silo communication is internal pod-to-pod
            })
            .WithEndpoint("orleans-gateway", endpoint =>
            {
                endpoint.Port = 30000;
                endpoint.TargetPort = 30000;
                endpoint.Protocol = ProtocolType.Tcp;
                endpoint.Transport = "tcp";
                endpoint.IsExternal = true; // Gateway accepts client connections
            })
            .WithEnvironment("Orleans__Endpoints__SiloPort", "11111")
            .WithEnvironment("Orleans__Endpoints__GatewayPort", "30000");

            // Also set environment variables that Orleans will use at runtime
            // These ensure Orleans binds to the correct ports even if Aspire doesn't generate them correctly
            silo.WithEnvironment("ORLEANS_SERVICE_ID", "greenlight-orleans")
                .WithEnvironment("ORLEANS_CLUSTER_ID", builder.Environment.EnvironmentName);
        }

        // Stable Kubernetes Service name for Silo in publish mode
        if (builder.ExecutionContext.IsPublishMode)
        {
            siloBuilder.PublishAsKubernetesService(res =>
            {
                if (res.Service is null)
                {
                    return;
                }

                res.Service.Metadata.Name = "silo";
                res.Service.Spec ??= new ServiceSpecV1();
                res.Service.Spec.Type = "ClusterIP";

                foreach (var port in res.Service.Spec.Ports)
                {
                    switch (port.Name)
                    {
                        case "http":
                            port.Port = 8080;
                            port.TargetPort = 8080;
                            break;
                        case "orleans-silo":
                            port.Port = 11111;
                            port.TargetPort = 11111;
                            break;
                        case "orleans-gateway":
                            port.Port = 30000;
                            port.TargetPort = 30000;
                            break;
                    }
                    port.Protocol = string.IsNullOrEmpty(port.Protocol) ? "TCP" : port.Protocol;
                }
            });
        }

        // Setup DocGen Frontend
        // In publish mode, exclude launch profile and manually specify HTTP endpoint to avoid duplicate ports
        var docGenFrontendBuilder = builder.ExecutionContext.IsPublishMode
            ? builder.AddProject<Projects.Microsoft_Greenlight_Web_DocGen>("web-docgen", launchProfileName: null)
                .WithHttpEndpoint(port: 8080, name: "http")
            : builder.AddProject<Projects.Microsoft_Greenlight_Web_DocGen>("web-docgen")
                .WithExternalHttpEndpoints();

        docGenFrontendBuilder
            .WithComputeEnvironment(k8s)
            .WithDotNetContainerDefaults()
            .WithConfigSection(envAzureAdConfigurationSection)
            .WithConfigSection(envServiceConfigurationConfigurationSection)
            .WithConfigSection(envConnectionStringsConfigurationSection)
            .WithConfigSection(envAzureConfigurationSection)
            .WithConfigSection(envKestrelConfigurationSection)
            .WithReference(azureDependencies.BlobStorage)
            .WithReference(azureDependencies.DocGenSql)
            .WithReference(azureDependencies.RedisResource)
            .WithReference(apiMain)
            .WithReference(orleans.Orleans.AsClient())
            .WithReference(orleans.Checkpointing)
            .WithReference(orleans.BlobStorage)
            .WithReference(orleans.ClusteringTable)
            .WaitFor(apiMain)
            .WaitForCompletion(dbSetupManager);

        // Add EventHub reference only if it exists (production or configured locally)
        if (azureDependencies.EventHub != null)
        {
            docGenFrontendBuilder.WithReference(azureDependencies.EventHub);
        }
        
        // Add Azure AI Search reference only if it exists (when not using Postgres)
        if (azureDependencies.AzureAiSearch != null)
        {
            docGenFrontendBuilder.WithReference(azureDependencies.AzureAiSearch);
        }

        if (azureDependencies.SignalR != null)
        {
            docGenFrontendBuilder.WithReference(azureDependencies.SignalR);
        }
        if (usePostgres && azureDependencies.KmVectorDb != null)
        {
            docGenFrontendBuilder.WithReference(azureDependencies.KmVectorDb);
        }


        var docGenFrontend = docGenFrontendBuilder;

        apiMain.WithReference(docGenFrontend); // Necessary for CORS policy creation

        // Apply Kubernetes resource configurations if specified
        ApplyResourceConfigurations(resourceConfigs, "db-setupmanager", dbSetupManager);
        ApplyResourceConfigurations(resourceConfigs, "api-main", apiMain);
        ApplyResourceConfigurations(resourceConfigs, "mcpserver-core", mcpServerCore);
        ApplyResourceConfigurations(resourceConfigs, "mcpserver-flow", mcpServerFlow);
        ApplyResourceConfigurations(resourceConfigs, "silo", silo);
        ApplyResourceConfigurations(resourceConfigs, "web-docgen", docGenFrontend);

        // Stable Kubernetes Service name for Web DocGen in publish mode
        if (builder.ExecutionContext.IsPublishMode)
        {
            docGenFrontendBuilder.PublishAsKubernetesService(res =>
            {
                if (res.Service is null)
                {
                    return;
                }

                res.Service.Metadata.Name = "web-docgen";
                res.Service.Spec ??= new ServiceSpecV1();
                res.Service.Spec.Type = "ClusterIP";

                foreach (var port in res.Service.Spec.Ports)
                {
                    if (port.Name == "http")
                    {
                        port.Port = 8080;
                        port.TargetPort = 8080;
                        port.Protocol = string.IsNullOrEmpty(port.Protocol) ? "TCP" : port.Protocol;
                    }
                }
            });
        }

        return new ProjectResources(dbSetupManager, apiMain, mcpServer, silo, docGenFrontend);
    }
    
    /// <summary>
    /// Parses Kubernetes resource configuration from environment variables
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Dictionary of service name to resource requirements</returns>
    private static Dictionary<string, ResourceConfig> ParseKubernetesResourceConfigs(IConfiguration configuration)
    {
        var resourcesConfigJson = configuration["KUBERNETES_RESOURCES_CONFIG"];
        if (string.IsNullOrEmpty(resourcesConfigJson))
            return new Dictionary<string, ResourceConfig>();
            
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ResourceConfig>>(resourcesConfigJson) 
                   ?? new Dictionary<string, ResourceConfig>();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Warning: Invalid KUBERNETES_RESOURCES_CONFIG JSON format: {ex.Message}");
            return new Dictionary<string, ResourceConfig>();
        }
    }
    
    /// <summary>
    /// Applies resource configurations to a project builder
    /// </summary>
    /// <param name="resourceConfigs">Resource configuration dictionary</param>
    /// <param name="serviceName">Service name to configure</param>
    /// <param name="projectBuilder">Project builder to apply configuration to</param>
    private static void ApplyResourceConfigurations(
        Dictionary<string, ResourceConfig> resourceConfigs,
        string serviceName,
        IResourceBuilder<ProjectResource> projectBuilder)
    {
        if (!resourceConfigs.TryGetValue(serviceName, out var config))
            return;

        try
        {
            // Convert to Kubernetes resource requirements
            var k8sRequirements = ToK8sResourceRequirements(config);

            var requestsCpu = k8sRequirements.Requests?["cpu"]?.ToString() ?? "";
            var requestsMemory = k8sRequirements.Requests?["memory"]?.ToString() ?? "";
            var limitsCpu = k8sRequirements.Limits?["cpu"]?.ToString() ?? "";
            var limitsMemory = k8sRequirements.Limits?["memory"]?.ToString() ?? "";

            Console.WriteLine($"Applying Kubernetes resources for {serviceName}: CPU({requestsCpu}/{limitsCpu}), Memory({requestsMemory}/{limitsMemory})");

            // Log replica configuration if present
            if (config.Replicas != null)
            {
                Console.WriteLine($"Replica configuration for {serviceName}: Min={config.Replicas.Min}, Max={config.Replicas.Max}");
            }

            // Apply resource configurations via PublishAsKubernetesService callback
            // This modifies the generated Kubernetes Deployment manifests during aspire publish
            projectBuilder.PublishAsKubernetesService(resource =>
            {
                // Get the workload (can be Deployment or StatefulSet)
                if (resource.Workload is not Aspire.Hosting.Kubernetes.Resources.Deployment deployment)
                    return;

                if (deployment.Spec?.Template?.Spec?.Containers == null || deployment.Spec.Template.Spec.Containers.Count == 0)
                    return;

                var container = deployment.Spec.Template.Spec.Containers[0];

                // Ensure Resources object exists
                container.Resources ??= new Aspire.Hosting.Kubernetes.Resources.ResourceRequirementsV1();

                // Add requests to the existing dictionary
                if (k8sRequirements.Requests != null && k8sRequirements.Requests.Count > 0)
                {
                    foreach (var kvp in k8sRequirements.Requests)
                    {
                        container.Resources.Requests[kvp.Key] = kvp.Value.ToString();
                    }
                }

                // Add limits to the existing dictionary
                if (k8sRequirements.Limits != null && k8sRequirements.Limits.Count > 0)
                {
                    foreach (var kvp in k8sRequirements.Limits)
                    {
                        container.Resources.Limits[kvp.Key] = kvp.Value.ToString();
                    }
                }

                // Apply replica count (use Min as the base replica count)
                // Note: HPA (Horizontal Pod Autoscaler) would be needed for actual min/max scaling
                if (config.Replicas?.Min != null && config.Replicas.Min > 0)
                {
                    deployment.Spec.Replicas = config.Replicas.Min;
                    Console.WriteLine($"Set replicas for {serviceName} to {config.Replicas.Min} (min from config)");
                }

                Console.WriteLine($"✅ Kubernetes resource requirements applied to {serviceName} deployment");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to apply resource configuration for {serviceName}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Converts resource configuration to Kubernetes resource requirements
    /// </summary>
    /// <param name="config">Resource configuration</param>
    /// <returns>Kubernetes resource requirements</returns>
    private static V1ResourceRequirements ToK8sResourceRequirements(ResourceConfig config)
    {
        var requirements = new V1ResourceRequirements();
        
        if (config.Requests != null)
        {
            requirements.Requests = new Dictionary<string, ResourceQuantity>();
            if (!string.IsNullOrEmpty(config.Requests.Cpu))
                requirements.Requests["cpu"] = new ResourceQuantity(config.Requests.Cpu);
            if (!string.IsNullOrEmpty(config.Requests.Memory))
                requirements.Requests["memory"] = new ResourceQuantity(config.Requests.Memory);
        }
        
        if (config.Limits != null)
        {
            requirements.Limits = new Dictionary<string, ResourceQuantity>();
            if (!string.IsNullOrEmpty(config.Limits.Cpu))
                requirements.Limits["cpu"] = new ResourceQuantity(config.Limits.Cpu);
            if (!string.IsNullOrEmpty(config.Limits.Memory))
                requirements.Limits["memory"] = new ResourceQuantity(config.Limits.Memory);
        }
        
        return requirements;
    }
    
    /// <summary>
    /// Resource configuration model for Kubernetes resource specifications
    /// </summary>
    /// <param name="Requests">Resource requests</param>
    /// <param name="Limits">Resource limits</param>
    /// <param name="Replicas">Replica configuration for scaling</param>
    internal record ResourceConfig(
        ResourceSpec? Requests,
        ResourceSpec? Limits,
        ReplicaSpec? Replicas
    );

    /// <summary>
    /// Resource specification for CPU and memory
    /// </summary>
    /// <param name="Cpu">CPU specification (e.g., "250m", "0.5")</param>
    /// <param name="Memory">Memory specification (e.g., "512Mi", "1Gi")</param>
    internal record ResourceSpec(
        string? Cpu,
        string? Memory
    );

    /// <summary>
    /// Replica specification for horizontal scaling
    /// </summary>
    /// <param name="Min">Minimum number of replicas</param>
    /// <param name="Max">Maximum number of replicas</param>
    internal record ReplicaSpec(
        int? Min,
        int? Max
    );
}
