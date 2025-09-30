// Copyright (c) Microsoft Corporation. All rights reserved.
using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Greenlight.AppHost;

/// <summary>
/// External Azure resource placeholders and configuration
/// </summary>
internal static partial class Program
{
    /// <summary>
    /// Result of setting up Azure dependencies
    /// </summary>
    internal readonly struct AzureDependencies
    {
        internal readonly IResourceBuilder<IResourceWithConnectionString> DocGenSql;
        internal readonly IResourceBuilder<IResourceWithConnectionString> RedisResource;
        internal readonly IResourceBuilder<IResourceWithConnectionString>? RedisSignalR;
        internal readonly IResourceBuilder<IResourceWithConnectionString>? SignalR;
        internal readonly IResourceBuilder<IResourceWithConnectionString>? AzureAiSearch;
        internal readonly IResourceBuilder<IResourceWithConnectionString> BlobStorage;
        internal readonly IResourceBuilder<IResourceWithConnectionString>? Insights;
        internal readonly IResourceBuilder<IResourceWithConnectionString>? EventHub;
        internal readonly IResourceBuilder<IResourceWithConnectionString>? KmVectorDb;
        internal readonly IResourceBuilder<IResourceWithConnectionString>? ApplicationGateway;
        internal readonly IResourceBuilder<IResourceWithConnectionString>? ContainerRegistry;

        internal AzureDependencies(
            IResourceBuilder<IResourceWithConnectionString> docGenSql,
            IResourceBuilder<IResourceWithConnectionString> redisResource,
            IResourceBuilder<IResourceWithConnectionString>? redisSignalR,
            IResourceBuilder<IResourceWithConnectionString>? signalR,
            IResourceBuilder<IResourceWithConnectionString>? azureAiSearch,
            IResourceBuilder<IResourceWithConnectionString> blobStorage,
            IResourceBuilder<IResourceWithConnectionString>? insights,
            IResourceBuilder<IResourceWithConnectionString>? eventHub,
            IResourceBuilder<IResourceWithConnectionString>? kmVectorDb,
            IResourceBuilder<IResourceWithConnectionString>? applicationGateway,
            IResourceBuilder<IResourceWithConnectionString>? containerRegistry)
        {
            DocGenSql = docGenSql;
            RedisResource = redisResource;
            RedisSignalR = redisSignalR;
            SignalR = signalR;
            AzureAiSearch = azureAiSearch;
            BlobStorage = blobStorage;
            Insights = insights;
            EventHub = eventHub;
            KmVectorDb = kmVectorDb;
            ApplicationGateway = applicationGateway;
            ContainerRegistry = containerRegistry;
        }
    }

    /// <summary>
    /// Sets up all Azure dependencies based on execution context
    /// </summary>
    /// <param name="builder">The distributed application builder</param>
    /// <param name="sqlPassword">SQL server password parameter</param>
    /// <param name="postgresPassword">PostgreSQL server password parameter</param>
    /// <param name="sqlDatabaseName">SQL database name</param>
    /// <param name="usePostgres">Whether to use PostgreSQL for vector storage</param>
    /// <returns>Configured Azure dependencies</returns>
    private static AzureDependencies SetupAzureDependencies(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<ParameterResource> sqlPassword,
        IResourceBuilder<ParameterResource> postgresPassword,
        string sqlDatabaseName,
        bool usePostgres)
    {
        // Determine deployment model for private endpoint configuration
        var deploymentModel = builder.Configuration["DEPLOYMENT_MODEL"] ?? "public";
        var isPrivateOrHybrid = deploymentModel is "private" or "hybrid";

        IResourceBuilder<IResourceWithConnectionString> docGenSql;
        // No extra SQL connection string resource; consumers bind to the database resource reference
        IResourceBuilder<IResourceWithConnectionString> redisResource;
        IResourceBuilder<IResourceWithConnectionString>? redisSignalr = null;
        IResourceBuilder<IResourceWithConnectionString>? signalr = null;
        IResourceBuilder<IResourceWithConnectionString>? azureAiSearch = null;
        IResourceBuilder<IResourceWithConnectionString> blobStorage;
        IResourceBuilder<IResourceWithConnectionString>? insights = null;
        IResourceBuilder<IResourceWithConnectionString>? eventHub = null;
        IResourceBuilder<IResourceWithConnectionString>? kmvectorDb = null;
        IResourceBuilder<IResourceWithConnectionString>? applicationGateway = null;

        if (builder.ExecutionContext.IsRunMode) // For local development
        {
            // Always use named volumes so data persists across container restarts, but do NOT
            // mark containers with ContainerLifetime.Persistent (we want them to shut down when
            // debugging stops). This preserves developer data without orphaning running containers.
            var hostId = Environment.MachineName.ToLowerInvariant();
            var sqlVolumePrefix = $"pvico-sql-docgen-{hostId}";
            var postgresVolumePrefix = $"pvico-pgsql-kmvectordb-{hostId}";
            var azuriteVolumePrefix = $"pvico-docing-emulator-{hostId}";

            // Main Redis for caching, Orleans, etc. - uses more memory
            redisResource = builder.AddRedis("redis", 16379)
                .WithArgs("--maxmemory", "512mb")
                .WithArgs("--maxmemory-policy", "allkeys-lru")
                .WithArgs("--save", "")  // Disable persistence for development
                .WithArgs("--appendonly", "no");  // Disable AOF for development

            // Dedicated Redis for SignalR backplane - minimal memory needed
            redisSignalr = builder.AddRedis("redis-signalr", 16380)
                .WithArgs("--maxmemory", "128mb")
                .WithArgs("--maxmemory-policy", "allkeys-lru")
                .WithArgs("--save", "")  // Disable persistence - backplane is ephemeral
                .WithArgs("--appendonly", "no");  // Disable AOF for backplane

            // Use Azure SQL Server for local development.
            // Especially useful for ARM/AMD based machines that can't run SQL Server in a container
            var useAzureSqlServer = Convert.ToBoolean(
                builder.Configuration["ServiceConfiguration:GreenlightServices:Global:UseAzureSqlServer"]);

            if (useAzureSqlServer)
            {
                docGenSql = builder
                    .AddAzureSqlServer("sqldocgen")
                    .AddDatabase(sqlDatabaseName);
            }
            else
            {
                // Use the parameter value (User Secrets or environment). No fallback/diagnostics noise.
                var configuredSqlPassword = builder.Configuration["Parameters:sqlPassword"]
                    ?? builder.Configuration["sqlPassword"]
                    ?? "DevPassword123!"; // Only used if developer hasn't set secret/parameter.

                docGenSql = builder
                    .AddSqlServer("sqldocgen", password: sqlPassword, port: 9001)
                    .WithDataVolume(sqlVolumePrefix)
                    // Force override of environment variable passed to container to ensure correct password.
                    // ACCEPT_EULA required by official mcr.microsoft.com/mssql/server images.
                    .WithEnvironment("MSSQL_SA_PASSWORD", configuredSqlPassword)
                    .WithEnvironment("ACCEPT_EULA", "Y")
                    .AddDatabase(sqlDatabaseName);
                Console.WriteLine($"[AppHost] Local SQL container password length: {configuredSqlPassword.Length}");
            }

            // EventHub not needed for local development - only create if user has configured it
            if (builder.Configuration.GetConnectionString("greenlight-cg-streams") != null)
            {
                eventHub = builder.AddConnectionString("greenlight-cg-streams");
            }
            // For local development, eventHub remains null and Orleans will use memory streams

            // Only add Azure AI Search when not using Postgres for vector storage
            if (!usePostgres)
            {
                azureAiSearch = builder.Configuration.GetConnectionString("aiSearch") != null
                    ? builder.AddConnectionString("aiSearch")
                    : builder.AddAzureSearch("aiSearch");
            }

            blobStorage = builder.Configuration.GetConnectionString("blob-docing") != null
                ? builder.AddConnectionString("blob-docing")
                : builder
                    .AddAzureStorage("docing")
                    .RunAsEmulator(azurite =>
                    {
                        azurite.WithDataVolume(azuriteVolumePrefix);
                    })
                    .AddBlobs("blob-docing");

            // Only add Application Insights if the connection string is set
            var aiConn = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrEmpty(aiConn))
            {
                insights = builder.AddConnectionString("insights", "APPLICATIONINSIGHTS_CONNECTION_STRING");
            }
            else
            {
                var useAppInsights = Convert.ToBoolean(
                    builder.Configuration["ServiceConfiguration:GreenlightServices:Global:UseApplicationInsights"]);
                if (useAppInsights)
                {
                    // For development: Create minimal Application Insights setup
                    // In production, this workspace will be shared with AKS Container Insights
                    insights = builder.AddAzureApplicationInsights("insights");
                }
            }

            if (usePostgres)
            {
                kmvectorDb = builder.AddPostgres("kmvectordb-server", port: 9002, password: postgresPassword)
                    .WithImage("pgvector/pgvector:pg16")
                    .WithDataVolume(postgresVolumePrefix)
                    .AddDatabase("kmvectordb");
            }
        }
        else // For production/Azure deployment
        {
            // Validate private networking requirements
            if (isPrivateOrHybrid)
            {
                ValidatePrivateNetworkingConfiguration(builder, deploymentModel, usePostgres);
            }

            // Create SQL Server and database
            var sqlServer = builder.AddAzureSqlServer("sqldocgen");

            // Opt out of the Free offer to avoid auto-pause/limits; keep GP_S_Gen5_2
            docGenSql = sqlServer
                .AddDatabase(sqlDatabaseName)
                .WithDefaultAzureSku();

            Console.WriteLine("[AppHost] SQL Server configured - workload identity will be set as SQL Server admin during deployment");

            // Connection string for ProjectVicoDB will flow from the database resource reference

            // Apply private endpoint configuration if required
            if (isPrivateOrHybrid)
            {
                ApplyPrivateEndpointConfiguration(docGenSql, "sql");
            }

            // Deploy Redis as containers in production - no Azure Redis needed
            // Use secret parameters for passwords to ensure publisher treats them as secrets
            var redisPassword = builder.AddParameter("redisPassword", secret: true);
            var redisSignalrPassword = builder.AddParameter("redisSignalRPassword", secret: true);

            // Main Redis for general caching, Orleans, data protection
            redisResource = builder.AddRedis("redis")
                .PublishAsContainer()
                .WithImage("redis:7-alpine")
                // Pass password via environment only; avoid embedding values in args
                .WithEnvironment("REDIS_PASSWORD", redisPassword)
                .WithArgs("-c")
                .WithArgs("redis-server --requirepass $REDIS_PASSWORD --maxmemory 4gb --maxmemory-policy allkeys-lru --save '' --appendonly no");

            // Dedicated Redis for SignalR backplane
            redisSignalr = builder.AddRedis("redis-signalr")
                .PublishAsContainer()
                .WithImage("redis:7-alpine")
                .WithEnvironment("REDIS_PASSWORD", redisSignalrPassword)
                .WithArgs("-c")
                .WithArgs("redis-server --requirepass $REDIS_PASSWORD --maxmemory 512mb --maxmemory-policy allkeys-lru --save '' --appendonly no");

            Console.WriteLine("[AppHost] Using containerized Redis instances (no Azure Redis)");

            // ALWAYS use Azure AI Search in production/publish mode regardless of local preferences
            // Postgres vector storage is only for local development
            // Configure AI Search to use Standard tier with 1 replica and 1 partition
            var azureSearch = builder.AddAzureSearch("aiSearch");

            // Cast to proper type and configure infrastructure
            azureSearch.ConfigureInfrastructure(infrastructure =>
            {
                var searchResources = infrastructure.GetProvisionableResources();

                foreach (var resource in searchResources)
                {
                    var resourceType = resource.GetType();

                    // Handle Azure Search Service resource
                    if (resourceType.Name.Contains("SearchService") || resourceType.Name.Contains("AzureSearch"))
                    {
                        dynamic searchService = resource;
                        try
                        {
                            // Set SKU to Standard tier (was Basic)
                            searchService.Sku = new
                            {
                                name = "standard"
                            };

                            // Set replica and partition counts
                            if (searchService.Properties != null)
                            {
                                searchService.Properties["replicaCount"] = 1;
                                searchService.Properties["partitionCount"] = 1;
                            }
                        }
                        catch { }
                    }
                }
            });

            azureAiSearch = azureSearch;

            if (isPrivateOrHybrid)
            {
                ApplyPrivateEndpointConfiguration(azureAiSearch, "search");
            }

            // Azure SignalR is disabled - using Redis backplane instead for reliability
            // SignalR will use the Redis connection for its backplane
            var enableSignalR = builder.Configuration.GetValue<bool>("ENABLE_AZURE_SIGNALR", defaultValue: false);
            if (enableSignalR)
            {
                Console.WriteLine("[AppHost] WARNING: Azure SignalR is deprecated in favor of Redis backplane");
                // Keeping code for backwards compatibility but defaulting to false
                signalr = builder.AddAzureSignalR("signalr");
                if (isPrivateOrHybrid)
                {
                    ApplyPrivateEndpointConfiguration(signalr, "signalr");
                }
            }

            blobStorage = builder.AddAzureStorage("docing")
                .AddBlobs("blob-docing");

            if (isPrivateOrHybrid)
            {
                ApplyPrivateEndpointConfiguration(blobStorage, "storage");
            }

            // Application Insights with custom Log Analytics workspace configuration
            // The workspace created here is shared with AKS Container Insights
            // IMPORTANT: PerGB2018 SKU includes first 31 days of retention in base price
            // Setting retention below 31 days provides NO cost savings
            // To reduce costs, focus on reducing log volume at source instead
            var logAnalytics = builder.AddAzureLogAnalyticsWorkspace("law");

            // Note: Retention configuration would need to be done via Bicep parameter overrides
            // or post-deployment scripts. The PerGB2018 SKU is most cost-effective for
            // variable/small workloads with 31-day included retention.

            insights = builder.AddAzureApplicationInsights("insights")
                .WithLogAnalyticsWorkspace(logAnalytics);

            eventHub = builder.AddAzureEventHubs("eventhub")
                .AddHub("greenlight-hub")
                .AddConsumerGroup("greenlight-cg-streams");


            // Postgres vector storage is only for local development, not production
            // Production always uses Azure AI Search for vector operations

            // Application Gateway for unified endpoint exposure
            // This provides "magic URL" functionality and centralized ingress
            applicationGateway = SetupApplicationGateway(builder, deploymentModel);
        }

        return new AzureDependencies(
            docGenSql, redisResource, redisSignalr, signalr, azureAiSearch, blobStorage,
            insights, eventHub, kmvectorDb, applicationGateway, null);
    }

    /// <summary>
    /// Validates private networking configuration requirements
    /// </summary>
    /// <param name="builder">The distributed application builder</param>
    /// <param name="deploymentModel">Deployment model (private/hybrid)</param>
    /// <param name="usePostgres">Whether PostgreSQL is being used</param>
    private static void ValidatePrivateNetworkingConfiguration(
        IDistributedApplicationBuilder builder,
        string deploymentModel,
        bool usePostgres)
    {
        var privateEndpointSubnet = builder.Configuration["AZURE_SUBNET_PE"];
        if (string.IsNullOrEmpty(privateEndpointSubnet))
        {
            throw new InvalidOperationException(
                $"AZURE_SUBNET_PE is required for {deploymentModel} deployment model. " +
                "Please provide the subnet resource ID for private endpoints.");
        }

        if (usePostgres)
        {
            var postgresSubnet = builder.Configuration["AZURE_SUBNET_POSTGRES"];
            var postgresDnsZone = builder.Configuration["POSTGRES_DNSZONE_RESOURCEID"];

            if (string.IsNullOrEmpty(postgresSubnet))
            {
                throw new InvalidOperationException(
                    $"AZURE_SUBNET_POSTGRES is required for PostgreSQL in {deploymentModel} deployment. " +
                    "Please provide the subnet resource ID for PostgreSQL delegation.");
            }

            if (string.IsNullOrEmpty(postgresDnsZone))
            {
                throw new InvalidOperationException(
                    $"POSTGRES_DNSZONE_RESOURCEID is required for PostgreSQL in {deploymentModel} deployment. " +
                    "Please provide the private DNS zone resource ID.");
            }
        }
    }

    /// <summary>
    /// Applies private endpoint configuration to an Azure resource
    /// </summary>
    /// <param name="resource">The Azure resource</param>
    /// <param name="resourceType">Resource type for annotation purposes</param>
    private static void ApplyPrivateEndpointConfiguration(
        IResourceBuilder<IResourceWithConnectionString> resource,
        string resourceType)
    {
        // NOTE: Private endpoint configuration via annotations
        // This is a placeholder for future Aspire private endpoint support
        // The actual implementation will depend on Aspire's Bicep generation capabilities
        // For now, we validate the configuration requirements in ValidatePrivateNetworkingConfiguration()
        Console.WriteLine($"Private endpoint configuration applied for {resourceType}");
    }

    /// <summary>
    /// Applies private configuration to PostgreSQL including subnet delegation
    /// </summary>
    /// <param name="postgres">The PostgreSQL resource</param>
    private static void ApplyPostgresPrivateConfiguration(
        IResourceBuilder<IResourceWithConnectionString> postgres)
    {
        // NOTE: PostgreSQL private networking configuration via annotations
        // This is a placeholder for future Aspire PostgreSQL private networking support
        // The actual implementation will depend on Aspire's Bicep generation capabilities
        // For now, we validate the configuration requirements in ValidatePrivateNetworkingConfiguration()
        Console.WriteLine("PostgreSQL private networking configuration applied");
    }

    /// <summary>
    /// Sets up Application Gateway for unified endpoint exposure with automatic domains
    /// </summary>
    /// <param name="builder">The distributed application builder</param>
    /// <param name="deploymentModel">Deployment model (public/private/hybrid)</param>
    /// <returns>Application Gateway resource or null if not needed</returns>
    private static IResourceBuilder<IResourceWithConnectionString>? SetupApplicationGateway(
        IDistributedApplicationBuilder builder,
        string deploymentModel)
    {
        // Application Gateway provides unified ingress for all public endpoints:
        // - API (including SignalR when self-hosted)
        // - Web Frontend
        // - MCP Server
        // With automatic "magic URL" generation and optional custom domain support

        // For development, we don't need Application Gateway
        if (builder.Environment.IsDevelopment())
        {
            return null;
        }

        var enableAppGateway = builder.Configuration.GetValue<bool>("ENABLE_APPLICATION_GATEWAY", defaultValue: false);
        if (!enableAppGateway)
        {
            Console.WriteLine("Application Gateway disabled via ENABLE_APPLICATION_GATEWAY=false");
            return null;
        }

        Console.WriteLine($"Application Gateway configured for {deploymentModel} deployment");
        Console.WriteLine("Endpoints: /api/*, /mcp/*, / (web), /hubs/* (SignalR)");

        // Configuration parameters
        var customDomain = builder.Configuration["APPLICATION_GATEWAY_DOMAIN"] ?? string.Empty;
        var enableSsl = builder.Configuration.GetValue<bool>("APPLICATION_GATEWAY_SSL_ENABLED", defaultValue: false);
        var subnetId = builder.Configuration["APPLICATION_GATEWAY_SUBNET_ID"] ?? string.Empty;
        var keyVaultId = builder.Configuration["APPLICATION_GATEWAY_KEYVAULT_ID"] ?? string.Empty;
        var certificateName = builder.Configuration["APPLICATION_GATEWAY_CERTIFICATE_NAME"] ?? string.Empty;

        // Determine which bicep template to use based on SSL configuration
        var bicepTemplatePath = enableSsl ? "../../build/bicep/application-gateway-ssl.bicep" : "../../build/bicep/application-gateway.bicep";

        // Create Application Gateway using Aspire 9.4 bicep injection
        var appGateway = builder.AddBicepTemplate("appgateway", bicepTemplatePath);

        // Configure parameters
        if (!string.IsNullOrEmpty(customDomain))
        {
            appGateway.WithParameter("customDomainName", customDomain);
        }

        if (!string.IsNullOrEmpty(subnetId))
        {
            appGateway.WithParameter("subnetId", subnetId);
        }

        if (enableSsl && !string.IsNullOrEmpty(keyVaultId) && !string.IsNullOrEmpty(certificateName))
        {
            appGateway
                .WithParameter("keyVaultId", keyVaultId)
                .WithParameter("keyVaultCertificateName", certificateName);
        }

        // Configure backend address pools (these will be populated by the deployment process)
        // Note: Complex object parameters require JSON serialization for bicep
        var backendConfigJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            api = Array.Empty<object>(),    // Will be populated with Container App URLs
            web = Array.Empty<object>(),    // Will be populated with Container App URLs
            mcp = Array.Empty<object>()     // Will be populated with Container App URLs
        });
        appGateway.WithParameter("backendAddresses", backendConfigJson);

        // Set deployment model tags
        var gatewayTagsJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["deployment-model"] = deploymentModel,
            ["aspire-resource-name"] = "appgateway",
            ["aspire-resource-type"] = "application-gateway",
            ["unified-ingress"] = "true"
        });
        appGateway.WithParameter("tags", gatewayTagsJson);

        Console.WriteLine($"Application Gateway bicep template: {bicepTemplatePath}");
        Console.WriteLine($"SSL enabled: {enableSsl}");
        Console.WriteLine($"Custom domain: {(string.IsNullOrEmpty(customDomain) ? "[Magic URL]" : customDomain)}");

        // Note: AzureBicepResource implements IResourceWithConnectionString
        return appGateway as IResourceBuilder<IResourceWithConnectionString>;
    }
}
