using Aspire.Azure.Search.Documents;
using Aspire.Azure.Storage.Blobs;
using AutoMapper;
using Azure.AI.OpenAI;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.DocumentProcess.Dynamic;
using Microsoft.Greenlight.Shared.DocumentProcess.Dynamic.Generation;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Exporters;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management;
using Microsoft.Greenlight.Shared.Management.Configuration;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Plugins;
using Microsoft.Greenlight.Shared.Repositories;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Validation;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Services.Search.Extensions;
using Microsoft.Greenlight.Shared.Services.Search.Providers;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Microsoft.SemanticKernel;
// legacy SK chat service still used in some factories
// legacy SK connectors
// legacy SK embeddings (to be removed later)
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Azure;
using Azure.Core.Extensions;
using Npgsql;
using Orleans.Configuration;
using Orleans.Serialization;
using StackExchange.Redis;
using StackExchange.Redis.Configuration;
using System.ClientModel;
using System.Reflection;
using Microsoft.Greenlight.Shared.Services.Caching;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Extensions;
#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001

/// <summary>
/// Provides extension methods for adding Greenlight services to the host application builder.
/// </summary>
public static class BuilderExtensions
{
    /// <summary>
    /// Sets up the database context and configuration provider.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The updated host application builder.</returns>
    public static IHostApplicationBuilder AddGreenlightDbContextAndConfiguration(this IHostApplicationBuilder builder)
    {
        // Get service configuration options from static configuration
        var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName)
            .Get<ServiceConfigurationOptions>()!;

        // Add the database context
        builder.AddDocGenDbContext(serviceConfigurationOptions);

        // Skip database configuration provider setup if running in "Microsoft.Greenlight.SetupManager.DB"
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (entryAssemblyName != "Microsoft.Greenlight.SetupManager.DB")
        {
            // Create the source first. This contains no build logic.
            var configSource = new EfCoreConfigurationProviderSource();

            // Add the database configuration provider
            builder.Services.AddSingleton<EfCoreConfigurationProvider>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<EfCoreConfigurationProvider>>();
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ServiceConfigurationOptions>>();
                var configuration = (IConfigurationRoot)sp.GetRequiredService<IConfiguration>();
                var dbContextFactory = sp.GetRequiredService<IDbContextFactory<DocGenerationDbContext>>();

                var provider = new EfCoreConfigurationProvider(dbContextFactory, logger, optionsMonitor, configuration);

                // Store the reference to this instance in the source
                configSource.SetProviderInstance(provider);

                return provider;
            });

            // Add the EfCoreConfigurationProvider to the configuration sources
            builder.Configuration.Add(configSource);

        }

        // Add Postgres client
        if (serviceConfigurationOptions.GreenlightServices.Global.UsePostgresMemory)
        {
            builder.AddNpgsqlDataSource("kmvectordb", configureDataSourceBuilder: dataSourceBuilder =>
            {
                dataSourceBuilder.UseVector();
            });
        }


        return builder;
    }

    /// <summary>
    /// Adds Greenlight services to the host application builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="credentialHelper">The Azure credential helper.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <returns>The updated host application builder.</returns>
    public static IHostApplicationBuilder AddGreenlightServices(this IHostApplicationBuilder builder,
        AzureCredentialHelper credentialHelper, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        builder.Services.AddAutoMapper(typeof(DocumentProcessInfoProfile));

        builder.AddGreenLightRedisClient("redis", credentialHelper, serviceConfigurationOptions);

        // Add HybridCache using configured IDistributedCache (Redis) as L2, memory as L1
        builder.Services.AddHybridCache();
        builder.Services.TryAddSingleton<IAppCache, AppCache>();

        // Common services and dependencies
        
        // Only add Azure AI Search client when not using Postgres for vector storage
        if (!serviceConfigurationOptions.GreenlightServices.Global.UsePostgresMemory)
        {
            builder.AddAzureSearchClient("aiSearch",
                configureSettings: delegate (AzureSearchSettings settings)
                {
                    settings.Credential = credentialHelper.GetAzureCredential();
                },
                configureClientBuilder: delegate (IAzureClientBuilder<SearchIndexClient, SearchClientOptions> clientBuilder)
                {
                    // Configure audience for Azure Government if using Azure Government authority
                    var azureInstance = builder.Configuration["AzureAd:Instance"];
                    if (!string.IsNullOrEmpty(azureInstance) &&
                        azureInstance.Contains(AzureAuthorityHosts.AzureGovernment.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        // Set Azure Government audience for Azure AI Search
                        clientBuilder.ConfigureOptions(options =>
                        {
                            options.Audience = SearchAudience.AzureGovernment;
                        });
                    }
                });
        }

        builder.AddKeyedAzureBlobClient("blob-docing", configureSettings: delegate (AzureStorageBlobsSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddKeyedAzureBlobClient("blob-orleans", configureSettings: delegate (AzureStorageBlobsSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddKeyedAzureTableClient("clustering", settings =>
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddKeyedAzureTableClient("checkpointing", settings =>
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        // Event Hub used by Orleans client and server for Orleans streaming

        builder.AddAzureEventHubConsumerClient("greenlight-cg-streams", configureSettings =>
        {
            configureSettings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddAzureEventHubProducerClient("greenlight-cg-streams", configureSettings =>
        {
            configureSettings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddAzureEventHubBufferedProducerClient("greenlight-cg-streams", configureSettings =>
        {
            configureSettings.Credential = credentialHelper.GetAzureCredential();
        });


        // Register Azure OpenAI Client - optional for system startup
        // If connection string is missing, the system will start without AI features
        builder.Services.AddKeyedSingleton<AzureOpenAIClient?>("openai-planner", (sp, obj) =>
            {
                var connectionString = builder.Configuration.GetConnectionString("openai-planner");
                if (string.IsNullOrEmpty(connectionString))
                {
                    var logger = sp.GetRequiredService<ILogger<AzureOpenAIClient>>();
                    logger.LogWarning("OpenAI connection string (openai-planner) is not configured. AI features will be unavailable until configured via the Configuration UI.");
                    return null;
                }

                try
                {
                    var connectionInfo = AzureOpenAIConnectionStringParser.Parse(connectionString);

                    // Create client with appropriate authentication
                    return string.IsNullOrEmpty(connectionInfo.Key)
                        ? new AzureOpenAIClient(new Uri(connectionInfo.Endpoint), credentialHelper.GetAzureCredential(), new AzureOpenAIClientOptions()
                        {
                            NetworkTimeout = 30.Minutes()
                        })
                        : new AzureOpenAIClient(new Uri(connectionInfo.Endpoint), new ApiKeyCredential(connectionInfo.Key), new AzureOpenAIClientOptions()
                        {
                            NetworkTimeout = 30.Minutes()
                        });
                }
                catch (Exception ex)
                {
                    var logger = sp.GetRequiredService<ILogger<AzureOpenAIClient>>();
                    logger.LogError(ex, "Failed to create Azure OpenAI client. AI features will be unavailable.");
                    return null;
                }
            });

        builder.Services.AddTransient<DynamicDocumentProcessServiceFactory>();

        builder.Services.AddTransient<AzureFileHelper>();
        
        // Only add SearchClientFactory when not using Postgres for vector storage
        if (!serviceConfigurationOptions.GreenlightServices.Global.UsePostgresMemory)
        {
            builder.Services.AddSingleton<SearchClientFactory>();
        }

        // File storage services
        builder.Services.AddTransient<IFileStorageServiceFactory, FileStorageServiceFactory>();
        builder.Services.AddTransient<IFileUrlResolverService, FileUrlResolverService>();

        builder.Services.AddTransient<IDocumentProcessInfoService, DocumentProcessInfoService>();
        builder.Services.AddTransient<IFlowTaskTemplateService, FlowTaskTemplateService>();
        builder.Services.AddTransient<IPromptInfoService, PromptInfoService>();
        builder.Services.AddTransient<ISystemPromptInfoService, SystemPromptInfoService>();
        builder.Services.AddTransient<IDocumentLibraryInfoService, DocumentLibraryInfoService>();

        // Register the Kernel Memory Instance Factory for Document Libraries and text extraction
        builder.Services.AddSingleton<KernelMemoryInstanceContainer>();
        // This can be scoped because it relies on the singleton KernelMemoryInstanceContainer to keep track of the instances
        builder.Services.AddTransient<IKernelMemoryInstanceFactory, KernelMemoryInstanceFactory>();

        // Text extraction service for content references using kernel memory
        builder.Services.AddTransient<IKernelMemoryTextExtractionService, KernelMemoryTextExtractionService>();

        builder.Services.AddKeyedTransient<IDocumentExporter, WordDocumentExporter>("IDocumentExporter-Word");

        // Register plugin source reference collector using Redis-backed implementation
        builder.Services.AddSingleton<IPluginSourceReferenceCollector, PluginSourceReferenceCollector>();
        builder.Services.AddKeyedTransient<IFunctionInvocationFilter, InputOutputTrackingPluginInvocationFilter>(
            "InputOutputTrackingPluginInvocationFilter");

        builder.Services.AddKeyedTransient<IFunctionInvocationFilter, PluginExecutionLoggingFilter>(
            "PluginExecutionLoggingFilter");

        // Vector store provider selection based on UsePostgresMemory flag

        // Register the appropriate VectorStore implementation based on UsePostgresMemory
        if (serviceConfigurationOptions.GreenlightServices.Global.UsePostgresMemory)
        {
            // Register PostgreSQL VectorStore - use the kmvectordb connection
            builder.Services.AddPostgresVectorStore(connectionString =>
            {
                return builder.Configuration.GetConnectionString("kmvectordb") ??
                       throw new InvalidOperationException("kmvectordb connection string is required for PostgreSQL vector store");
            });
        }
        else
        {
            // Register Azure AI Search VectorStore - use the existing configured SearchIndexClient
            // This leverages the already configured "aiSearch" client with proper Azure credentials (including Azure US Government support)
            builder.Services.AddAzureAISearchVectorStore();
        }

        builder.Services.AddTransient<ISemanticKernelVectorStoreProvider, SemanticKernelVectorStoreProvider>();

        // Document ingestion services for Semantic Kernel Vector Store
        builder.Services.AddVectorStoreServices();

        builder.AddRepositories();

        builder.Services.AddTransient<ValidationStepExecutionLogicFactory>();

        // Add all IValidationStepExecutionLogic implementations
        builder.Services
            .AddKeyedTransient<IValidationStepExecutionLogic, ParallelByOuterChapterValidationStepExecutionLogic>(
                nameof(ParallelByOuterChapterValidationStepExecutionLogic));

        builder.Services
            .AddKeyedTransient<IValidationStepExecutionLogic, ParallelFullDocumentValidationStepExecutionLogic>(
                nameof(ParallelFullDocumentValidationStepExecutionLogic));

        builder.Services
            .AddKeyedTransient<IValidationStepExecutionLogic, SequentialFullDocumentValidationStepExecutionLogic>(
                nameof(SequentialFullDocumentValidationStepExecutionLogic));



        // Content Reference Services
        builder.Services.AddTransient<IContentNodeService, ContentNodeService>();
        builder.Services.AddTransient<IContentReferenceService, ContentReferenceService>();
        builder.Services.AddTransient<IPromptDefinitionService, PromptDefinitionService>();

        // Register the factory
        builder.Services
            .AddTransient<IContentReferenceGenerationServiceFactory, ContentReferenceGenerationServiceFactory>();

        // Register content type specific generation services
        builder.Services
            .AddTransient<IContentReferenceGenerationService<GeneratedDocument>,
                GeneratedDocumentReferenceGenerationService>();
        builder.Services
            .AddTransient<IContentReferenceGenerationService<ExportedDocumentLink>,
                UploadedDocumentReferenceGenerationService>();
        builder.Services
            .AddTransient<IContentReferenceGenerationService<ReviewInstance>,
                ReviewContentReferenceGenerationService>();
        builder.Services
            .AddTransient<IContentReferenceGenerationService<Microsoft.Greenlight.Shared.Models.FileStorage.ExternalLinkAsset>,
                ExternalLinkAssetReferenceGenerationService>();

        // Context Builder that uses embeddings generation to build rag contexts from content references for user queries
        builder.Services.AddTransient<IRagContextBuilder, RagContextBuilder>();

        // Content Reference Vector Store repository
        builder.Services.AddTransient<IContentReferenceVectorRepository, ContentReferenceSemanticKernelVectorStoreRepository>();

        // Add the shared Dynamic Outline Service
        builder.Services.AddKeyedTransient<IDocumentOutlineService, DynamicDocumentOutlineService>(
            "Dynamic-IDocumentOutlineService");
        builder.Services.AddTransient<IDocumentOutlineService, DynamicDocumentOutlineService>();

        // ReviewKernelMemoryRepository deprecated: Reviews now use ContentReference pipeline exclusively.

        // Review services
        builder.Services.AddTransient<IReviewService, ReviewService>();

        // Add the plugin registry as a singleton (it hosts plugins)
        builder.Services.AddSingleton<IPluginRegistry, DefaultPluginRegistry>();

        return builder;
    }

    /// <summary>
    /// Adds standard hosted services that run on all hosts.
    /// This must be called AFTER Orleans Client setup (normally at the end of the builder).
    /// </summary>
    /// <param name="services">The Service Collection</param>
    /// <param name="addSignalrNotifiers">Whether to add the SignalR notifiers on this host</param>
    /// <returns></returns>
    public static IServiceCollection AddGreenlightHostedServices(this IServiceCollection services,
        bool addSignalrNotifiers = false)
    {
        services.AddConfigurationStreamNotifier();
        services.AddPluginStreamNotifier();
        services.AddFlowStreamNotifier();
        services.StartOrleansStreamSubscriberService();

        services.AddHostedService<DatabaseConfigurationRefreshService>();
        services.AddHostedService<ShutdownCleanupService>();
        return services;
    }

    /// <summary>
    /// Adds a Redis client to the host application builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="redisConnectionStringName">The name of the Redis connection string.</param>
    /// <param name="credentialHelper">The Azure credential helper.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <returns>The updated host application builder.</returns>
    public static IHostApplicationBuilder AddGreenLightRedisClient(this IHostApplicationBuilder builder,
        string redisConnectionStringName, AzureCredentialHelper credentialHelper,
        ServiceConfigurationOptions serviceConfigurationOptions)
    {
        // Simplified Redis client configuration for containerized Redis instances
        // We now use two internal Redis containers:
        // - "redis": Main instance for caching, Orleans, data protection
        // - "redis-signalr": Dedicated instance for SignalR backplane
        // Both use password authentication configured in the connection string

        builder.AddRedisClient(redisConnectionStringName);
        builder.AddRedisDistributedCache(redisConnectionStringName);

        /* LEGACY AZURE REDIS CONFIGURATION - Kept for reference if we need to revert
        if (AdminHelper.IsRunningInProduction())
        {
            var azureOptionsProvider = new AzureOptionsProvider();

            var configurationOptions = ConfigurationOptions.Parse(
                builder.Configuration.GetConnectionString(redisConnectionStringName) ??
                throw new InvalidOperationException("Couldn't find a redis connection string"));

            if (configurationOptions.EndPoints.Any(azureOptionsProvider.IsMatch))
            {
                configurationOptions.ConfigureForAzureWithTokenCredentialAsync(
                    credentialHelper.GetAzureCredential()).Wait();
            }

            builder.AddRedisClient(redisConnectionStringName,
                configureOptions: options => { options.Defaults = configurationOptions.Defaults; });

            builder.AddRedisDistributedCache(redisConnectionStringName,
                configureOptions: options => { options.Defaults = configurationOptions.Defaults; });
        }
        else
        {
            builder.AddRedisClient(redisConnectionStringName);
            builder.AddRedisDistributedCache(redisConnectionStringName);
        }
        */

        return builder;
    }


    /// <summary>
    /// Add an Orleans Silo to the Builder
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="credentialHelper"></param>
    /// <param name="clusterRole"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder AddGreenLightOrleansSilo(
        this IHostApplicationBuilder builder, AzureCredentialHelper credentialHelper,
        string clusterRole = "DefaultRole")
    {
        var eventHubConnectionString = builder.Configuration.GetConnectionString("greenlight-cg-streams");
        var checkPointTableStorageConnectionString = builder.Configuration.GetConnectionString("checkpointing");
        var orleansBlobStoreConnectionString = builder.Configuration.GetConnectionString("blob-orleans");

        var currentAssembly = Assembly.GetExecutingAssembly();

        builder.UseOrleans(siloBuilder =>
        {

            siloBuilder.Configure<SiloMessagingOptions>(options =>
            {
                options.ResponseTimeout = TimeSpan.FromMinutes(15);
                options.DropExpiredMessages = false;
            });

            siloBuilder.Configure<ClientMessagingOptions>(options =>
            {
                options.ResponseTimeout = TimeSpan.FromMinutes(15);
                options.DropExpiredMessages = false;
            });


            siloBuilder.Services.AddSerializer(serializerBuilder =>
            {
                serializerBuilder.AddJsonSerializer(DetectSerializableAssemblies);

                // Is there a way to add Orleans Serializers for referenced assemblies?
                serializerBuilder.AddAssembly(typeof(ChatMessageDTO).Assembly);
                serializerBuilder.AddAssembly(typeof(Microsoft.Greenlight.Shared.Models.ChatMessage).Assembly);
                serializerBuilder.AddAssembly(currentAssembly);
            });

            // Use in-memory streams for local development to avoid EventHub dependencies
            if (AdminHelper.IsRunningInProduction())
            {
                // Add EventHub-based streaming for high throughput in production
                siloBuilder.AddEventHubStreams("StreamProvider", (ISiloEventHubStreamConfigurator streamsConfigurator) =>
                {
                    streamsConfigurator.ConfigureEventHub(eventHubBuilder => eventHubBuilder.Configure(options =>
                    {
                        var eventHubNamespace = eventHubConnectionString!.Split("Endpoint=")[1].Split(":443/")[0];
                        var eventHubName = eventHubConnectionString!.Split("EntityPath=")[1].Split(";")[0];
                        var consumerGroup = eventHubConnectionString!.Split("ConsumerGroup=")[1].Split(";")[0];
                        options.ConfigureEventHubConnection(
                            eventHubNamespace,
                            eventHubName,
                            consumerGroup, credentialHelper.GetAzureCredential());

                    }));

                    streamsConfigurator.UseAzureTableCheckpointer(checkpointBuilder =>

                        checkpointBuilder.Configure(options =>
                        {
                            var tuple = AzureStorageHelper.ParseTableEndpointAndCredential(checkPointTableStorageConnectionString!);
                            var tableEndpoint = tuple.endpoint;
                            var sharedKey = tuple.sharedKeyCredential;
                            if (sharedKey != null)
                            {
                                options.TableServiceClient = new TableServiceClient(tableEndpoint, sharedKey);
                            }
                            else
                            {
                                options.TableServiceClient = new TableServiceClient(new Uri(checkPointTableStorageConnectionString!), credentialHelper.GetAzureCredential());
                            }
                            options.PersistInterval = TimeSpan.FromSeconds(10);
                        }));

                });
            }
            else
            {
                // Use memory streams with Redis PubSub store for local development
                // This enables stream sharing between hosts while using Orleans built-in infrastructure
                siloBuilder.AddMemoryStreams("StreamProvider");
            }

            // Configure PubSubStore - use Redis for local dev to enable stream sharing
            if (AdminHelper.IsRunningInProduction())
            {
                siloBuilder.AddAzureBlobGrainStorage("PubSubStore", options =>
                {
                    var tuple = AzureStorageHelper.ParseBlobEndpointAndCredential(orleansBlobStoreConnectionString!);
                    var blobEndpoint = tuple.endpoint;
                    var sharedKey = tuple.sharedKeyCredential;
                    if (sharedKey != null)
                    {
                        options.BlobServiceClient = new BlobServiceClient(blobEndpoint, sharedKey);
                    }
                    else
                    {
                        options.BlobServiceClient = new BlobServiceClient(new Uri(orleansBlobStoreConnectionString!), credentialHelper.GetAzureCredential());
                    }
                });
            }
            else
            {
                // Use Redis storage for PubSub in local development to enable stream sharing
                var redisConnectionString = builder.Configuration.GetConnectionString("redis");
                siloBuilder.AddRedisGrainStorage("PubSubStore", options =>
                {
                    options.ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString!);
                });
            }

            siloBuilder.AddAzureBlobGrainStorageAsDefault(options =>
            {
                options.ContainerName = "grain-storage";
                var tuple = AzureStorageHelper.ParseBlobEndpointAndCredential(orleansBlobStoreConnectionString!);
                var blobEndpoint = tuple.endpoint;
                var sharedKey = tuple.sharedKeyCredential;
                if (sharedKey != null)
                {
                    options.BlobServiceClient = new BlobServiceClient(blobEndpoint, sharedKey);
                }
                else
                {
                    options.BlobServiceClient = new BlobServiceClient(new Uri(orleansBlobStoreConnectionString!), credentialHelper.GetAzureCredential());
                }
            });

            siloBuilder.UseAzureTableReminderService(options =>
            {
                var tuple = AzureStorageHelper.ParseTableEndpointAndCredential(checkPointTableStorageConnectionString!);
                var tableEndpoint = tuple.endpoint;
                var sharedKey = tuple.sharedKeyCredential;
                if (sharedKey != null)
                {
                    options.TableServiceClient = new TableServiceClient(tableEndpoint, sharedKey);
                }
                else
                {
                    options.TableServiceClient = new TableServiceClient(new Uri(checkPointTableStorageConnectionString!), credentialHelper.GetAzureCredential());
                }
                options.TableName = "OrleansReminders";
            });

            // Configure clustering using Azure Table Storage - critical for silo discovery
            var clusteringConnectionString = builder.Configuration.GetConnectionString("clustering");
            if (!string.IsNullOrWhiteSpace(clusteringConnectionString))
            {
                siloBuilder.UseAzureStorageClustering(options =>
                {
                    var tuple = AzureStorageHelper.ParseTableEndpointAndCredential(clusteringConnectionString!);
                    var tableEndpoint = tuple.endpoint;
                    var sharedKey = tuple.sharedKeyCredential;

                    if (sharedKey != null)
                    {
                        options.TableServiceClient = new TableServiceClient(tableEndpoint, sharedKey);
                    }
                    else if (clusteringConnectionString.Contains('=')
                             || clusteringConnectionString.StartsWith("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase))
                    {
                        options.TableServiceClient = new TableServiceClient(clusteringConnectionString);
                    }
                    else if (Uri.TryCreate(clusteringConnectionString, UriKind.Absolute, out var endpointUri))
                    {
                        options.TableServiceClient = new TableServiceClient(endpointUri, credentialHelper.GetAzureCredential());
                    }
                    else
                    {
                        options.TableServiceClient = new TableServiceClient(clusteringConnectionString);
                    }

                    options.TableName = "OrleansSiloInstances"; // must match client clustering table name
                });
            }

            bool DetectSerializableAssemblies(Type arg)
            {
                // Check if the type is in any assembly starting with Microsoft.Greenlight.Grain or Microsoft.Greenlight.Shared
                // This is a bit of a hack, but it works for now
                var assemblyName = arg.Assembly.GetName().Name;
                return assemblyName != null &&
                       (assemblyName.StartsWith("Microsoft.Greenlight.Grain") ||
                        assemblyName.StartsWith("Microsoft.Greenlight.Shared"));
            }
        });

        return builder;
    }


    /// <summary>
    /// Adds the standardized Orleans Client to the Builder
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="credentialHelper"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder AddGreenlightOrleansClient(
        this IHostApplicationBuilder builder,
        AzureCredentialHelper credentialHelper)
    {
        var eventHubConnectionString = builder.Configuration.GetConnectionString("greenlight-cg-streams");
        var clusteringConnectionString = builder.Configuration.GetConnectionString("clustering");
        var loggerFactory = LoggerFactory.Create(cfg => cfg.AddConsole());
        var logger = loggerFactory.CreateLogger("Greenlight.OrleansClient");

        builder.UseOrleansClient(siloBuilder =>
        {
            // Ensure Client uses the same ClusterId/ServiceId as the primary Orleans Silo
            // Defaults match Aspire AppHost configuration
            siloBuilder.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = builder.Configuration["Orleans:ClusterId"] ?? "greenlight-cluster";
                options.ServiceId = builder.Configuration["Orleans:ServiceId"] ?? "greenlight-main-silo";
            });

            siloBuilder.Configure<ClientMessagingOptions>(options =>
            {
                options.ResponseTimeout = TimeSpan.FromMinutes(15);
                options.DropExpiredMessages = false;
            });

            siloBuilder.Configure<ClientMessagingOptions>(options =>
            {
                options.ResponseTimeout = TimeSpan.FromMinutes(15);
                options.DropExpiredMessages = false;
            });

            siloBuilder.UseConnectionRetryFilter(async (exception, token) =>
            {
                // Log the connection failure
                Console.WriteLine($"Orleans client connection failed: {exception.Message}");

                // Wait before retrying
                await Task.Delay(TimeSpan.FromSeconds(5), token);

                // Return true to retry the connection
                return true;
            });

            // Increase connection timeout
            siloBuilder.Configure<ClientMessagingOptions>(options =>
            {
                options.ResponseTimeout = TimeSpan.FromMinutes(5);
            });

            siloBuilder.Services.AddSerializer(serializerBuilder =>
            {
                serializerBuilder.AddJsonSerializer(DetectSerializableAssemblies);

                // Is there a way to add Orleans Serializers for referenced assemblies?
                serializerBuilder.AddAssembly(typeof(ChatMessageDTO).Assembly);
                serializerBuilder.AddAssembly(typeof(Microsoft.Greenlight.Shared.Models.ChatMessage).Assembly);

                // Get the currently executing assembly and add it
                var executingAssembly = Assembly.GetExecutingAssembly();
                serializerBuilder.AddAssembly(executingAssembly);
            });

            bool DetectSerializableAssemblies(Type arg)
            {
                // Check if the type is in any assembly starting with Microsoft.Greenlight.Grain or Microsoft.Greenlight.Shared
                // This is a bit of a hack, but it works for now
                var assemblyName = arg.Assembly.GetName().Name;
                return assemblyName != null &&
                       (assemblyName.StartsWith("Microsoft.Greenlight.Grain") ||
                        assemblyName.StartsWith("Microsoft.Greenlight.Shared"));
            }

            // If Aspire provided explicit gateway endpoints, prefer static clustering in local dev.
            // This avoids 127.0.0.1 misrouting issues and uses the S<ip> host address instead.
            var gatewaysSection = builder.Configuration.GetSection("Orleans:Gateways");
            var configuredStaticGateways = false;
            if (gatewaysSection.Exists() && gatewaysSection.GetChildren().Any())
            {
                try
                {
                    var gateways = new List<System.Net.IPEndPoint>();
                    foreach (var child in gatewaysSection.GetChildren())
                    {
                        var address = child["Address"]; // e.g., S172.19.176.1
                        var portStr = child["Port"];
                        if (!string.IsNullOrWhiteSpace(address) && int.TryParse(portStr, out var port))
                        {
                            // S-address includes a leading 'S' in Aspire; strip if present
                            var cleanAddress = address.StartsWith("S", StringComparison.OrdinalIgnoreCase)
                                ? address.Substring(1)
                                : address;
                            if (System.Net.IPAddress.TryParse(cleanAddress, out var ip))
                            {
                                gateways.Add(new System.Net.IPEndPoint(ip, port));
                            }
                        }
                    }

                    if (gateways.Count > 0)
                    {
                        siloBuilder.UseStaticClustering(gateways.ToArray());
                        logger.LogInformation("Orleans client: Using static clustering to gateways: {Gateways}", string.Join(", ", gateways.Select(g => $"{g.Address}:{g.Port}")));
                        configuredStaticGateways = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to configure static clustering from Orleans:Gateways. Will continue to membership configuration.");
                }
            }

            // Use Redis streams for local development, EventHub for production
            if (AdminHelper.IsRunningInProduction() &&
                !string.IsNullOrWhiteSpace(eventHubConnectionString) &&
                eventHubConnectionString.Contains("Endpoint=") &&
                eventHubConnectionString.Contains("EntityPath=") &&
                eventHubConnectionString.Contains("ConsumerGroup="))
            {
                // Configure Event Hubs streaming for production environments
                siloBuilder.AddEventHubStreams("StreamProvider", (IClusterClientEventHubStreamConfigurator configurator) =>
                {
                    configurator.ConfigureEventHub(eventHubBuilder => eventHubBuilder.Configure(options =>
                    {
                        var eventHubNamespace = eventHubConnectionString!.Split("Endpoint=")[1].Split(":443/")[0];
                        var eventHubName = eventHubConnectionString!.Split("EntityPath=")[1].Split(";")[0];
                        var consumerGroup = eventHubConnectionString!.Split("ConsumerGroup=")[1].Split(";")[0];

                        options.ConfigureEventHubConnection(
                            eventHubNamespace,
                            eventHubName,
                            consumerGroup, credentialHelper.GetAzureCredential());

                    }));
                });
            }
            else
            {
                // Use memory streams with Redis PubSub store for local development
                // This enables stream sharing between hosts while using Orleans built-in infrastructure
                siloBuilder.AddMemoryStreams("StreamProvider");
            }

            // Configure membership discovery so the client can find gateways.
            // Prefer Azure Table (also works with Azurite in local Aspire).
            if (!configuredStaticGateways && !string.IsNullOrWhiteSpace(clusteringConnectionString))
            {
                siloBuilder.UseAzureStorageClustering(options =>
                {
                    // Robustly construct TableServiceClient:
                    // - If we have a parsed endpoint + shared key, use those
                    // - Else, if the value looks like a connection string (contains '=') use it directly
                    // - Else, treat it as a URI and use DefaultAzureCredential
                    var tuple = AzureStorageHelper.ParseTableEndpointAndCredential(clusteringConnectionString!);
                    var tableEndpoint = tuple.endpoint;
                    var sharedKey = tuple.sharedKeyCredential;

                    if (sharedKey != null)
                    {
                        options.TableServiceClient = new TableServiceClient(tableEndpoint, sharedKey);
                        logger.LogInformation("Orleans client: Using Azure Table clustering with shared key at {Endpoint}", tableEndpoint);
                    }
                    else if (clusteringConnectionString.Contains('=')
                             || clusteringConnectionString.StartsWith("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase))
                    {
                        // Standard Azure/Azurite connection string
                        options.TableServiceClient = new TableServiceClient(clusteringConnectionString);
                        logger.LogInformation("Orleans client: Using Azure Table clustering via connection string");
                    }
                    else if (Uri.TryCreate(clusteringConnectionString, UriKind.Absolute, out var endpointUri))
                    {
                        // Endpoint with AAD/Managed Identity
                        options.TableServiceClient = new TableServiceClient(endpointUri, credentialHelper.GetAzureCredential());
                        logger.LogInformation("Orleans client: Using Azure Table clustering with AAD at {Endpoint}", endpointUri);
                    }
                    else
                    {
                        // Last resort: attempt connection string ctor (will throw early if invalid)
                        options.TableServiceClient = new TableServiceClient(clusteringConnectionString);
                        logger.LogInformation("Orleans client: Using Azure Table clustering via connection string (fallback)");
                    }

                    options.TableName = "OrleansSiloInstances"; // must match silo's AzureTable clustering table name
                });
            }
            else if (!configuredStaticGateways)
            {
                // Fallback for pure local dev without Azure Table membership
                // Uses the default localhost gateway (or override via Orleans__Endpoints__GatewayPort)
                if (int.TryParse(builder.Configuration["Orleans:Endpoints:GatewayPort"], out var gwPort))
                {
                    siloBuilder.UseLocalhostClustering(gatewayPort: gwPort);
                    logger.LogWarning("Orleans client: Falling back to localhost clustering on port {Port}", gwPort);
                }
                else
                {
                    siloBuilder.UseLocalhostClustering();
                    logger.LogWarning("Orleans client: Falling back to localhost clustering on default port");
                }
            }
        });

        return builder;
    }

    /// <summary>
    /// Adds repositories to the IHostApplicationBuilder.
    /// </summary>
    /// <param name="builder">The IHostApplicationBuilder to add the repositories to.</param>
    /// <returns>The updated IHostApplicationBuilder.</returns>
    private static IHostApplicationBuilder AddRepositories(this IHostApplicationBuilder builder)
    {
        // Add GenericRepository<T> itself
        builder.Services.AddScoped(typeof(GenericRepository<>));

        // Get the assembly containing the repositories
        var repositoryAssembly = typeof(GenericRepository<>).Assembly;

        // Register all classes in the specified namespace that inherit from GenericRepository<T>
        foreach (var type in repositoryAssembly.GetTypes())
        {
            if (type is { IsClass: true, IsAbstract: false, Namespace: "Microsoft.Greenlight.Shared.Repositories" })
            {
                var baseType = type.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(GenericRepository<>))
                    {
                        builder.Services.AddScoped(type);
                        break;
                    }
                    baseType = baseType.BaseType;
                }
            }
        }

        return builder;
    }


    /// <summary>
    /// Gets a required service for the specified document process.
    /// </summary>
    /// <typeparam name="T">The type of the service to get.</typeparam>
    /// <param name="sp">The service provider.</param>
    /// <param name="documentProcessInfo">The document process information.</param>
    /// <returns>The service instance if found; otherwise, throws an InvalidOperationException.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found for the specified document process.</exception>
    public static T GetRequiredServiceForDocumentProcess<T>(this IServiceProvider sp, DocumentProcessInfo documentProcessInfo)
    {
        var service = sp.GetRequiredServiceForDocumentProcess<T>(documentProcessInfo.ShortName);
        return service;
    }

    /// <summary>
    /// Gets a required service for the specified document process.
    /// </summary>
    /// <typeparam name="T">The type of the service to get.</typeparam>
    /// <param name="sp">The service provider.</param>
    /// <param name="documentProcessName">The name of the document process.</param>
    /// <returns>The service instance if found; otherwise, throws an InvalidOperationException.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found for the specified document process.</exception>
    private static T GetRequiredServiceForDocumentProcess<T>(this IServiceProvider sp, string documentProcessName)
    {
        var service = sp.GetServiceForDocumentProcess<T>(documentProcessName);
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} not found for document process {documentProcessName}");
        }
        return service;
    }


    /// <summary>
    /// Gets a service for the specified document process.
    /// </summary>
    /// <typeparam name="T">The type of the service to get.</typeparam>
    /// <param name="sp">The service provider.</param>
    /// <param name="documentProcessName">The name of the document process.</param>
    /// <returns>The service instance if found; otherwise, null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the document process info is not found and the document process name is not "Reviews".</exception>
    private static T? GetServiceForDocumentProcess<T>(this IServiceProvider sp, string documentProcessName)
    {
        T? service = default;


        var documentProcessInfoService = sp.GetRequiredService<IDocumentProcessInfoService>();
        var dbContext = sp.GetRequiredService<DocGenerationDbContext>();
        var mapper = sp.GetRequiredService<IMapper>();

        DocumentProcessInfo? documentProcessInfo = null;
        try
        {
            documentProcessInfo = documentProcessInfoService.GetDocumentProcessInfoByShortName(documentProcessName);
        }
        catch
        {
            var dynamicDocumentProcess = dbContext.DynamicDocumentProcessDefinitions
                .AsNoTracking()
                .FirstOrDefault(x => x.ShortName == documentProcessName);

            if (dynamicDocumentProcess != null)
            {
                documentProcessInfo = mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
            }
        }

        if (documentProcessInfo == null && documentProcessName != "Reviews")
        {
            throw new InvalidOperationException($"Document process info not found for {documentProcessName}");
        }

        var dynamicServiceKey = $"Dynamic-{typeof(T).Name}";
        var documentProcessServiceKey = $"{documentProcessName}-{typeof(T).Name}";

        // We try to get service for the specific document process first from the DynamicDocumentProcessServiceFactory
        if (documentProcessInfo?.Source == ProcessSource.Dynamic)
        {
            using var scope = sp.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<DynamicDocumentProcessServiceFactory>();

            service = factory.GetServiceAsync<T>(documentProcessName).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        // If we didn't find the service in the factory, we try to get it from the scope in descending order of specificity
        if (service == null)
        {
            // Try to get a scoped service for the specific document process,
            // then the dynamic service, then the default service,
            // then finally a service with no key.
            service = sp.GetKeyedService<T>(documentProcessServiceKey) ??
                      sp.GetKeyedService<T>(dynamicServiceKey) ??
                      sp.GetKeyedService<T>($"Default-{typeof(T).Name}") ??
                      sp.GetService<T>();
        }

        return service;
    }

    /// <summary>
    /// Gets a service for the specified document process asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the service to get.</typeparam>
    /// <param name="sp">The service provider.</param>
    /// <param name="documentProcessName">The name of the document process.</param>
    /// <returns>The service instance if found; otherwise, null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the document process info is not found and the document process name is not "Reviews".</exception>
    public static async Task<T?> GetServiceForDocumentProcessAsync<T>(this IServiceProvider sp, string documentProcessName)
    {
        T? service = default;

        var documentProcessInfoService = sp.GetRequiredService<IDocumentProcessInfoService>();
        var dbContext = sp.GetRequiredService<DocGenerationDbContext>();
        var mapper = sp.GetRequiredService<IMapper>();

        DocumentProcessInfo? documentProcessInfo = null;
        try
        {
            documentProcessInfo = await documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);
        }
        catch
        {
            var dynamicDocumentProcess = await dbContext.DynamicDocumentProcessDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShortName == documentProcessName);

            if (dynamicDocumentProcess != null)
            {
                documentProcessInfo = mapper.Map<DocumentProcessInfo>(dynamicDocumentProcess);
            }
        }

        if (documentProcessInfo == null && documentProcessName != "Reviews")
        {
            throw new InvalidOperationException($"Document process info not found for {documentProcessName}");
        }

        var dynamicServiceKey = $"Dynamic-{typeof(T).Name}";
        var documentProcessServiceKey = $"{documentProcessName}-{typeof(T).Name}";

        // We try to get service for the specific document process first from the DynamicDocumentProcessServiceFactory
        if (documentProcessInfo?.Source == ProcessSource.Dynamic)
        {
            using var scope = sp.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<DynamicDocumentProcessServiceFactory>();
            service = await factory.GetServiceAsync<T>(documentProcessName);
        }

        // If we didn't find the service in the factory, we try to get it from the scope in descending order of specificity
        if (service == null)
        {
            // Try to get a scoped service for the specific document process,
            // then the dynamic service, then the default service,
            // then finally a service with no key.
            service = sp.GetKeyedService<T>(documentProcessServiceKey) ??
                      sp.GetKeyedService<T>(dynamicServiceKey) ??
                      sp.GetKeyedService<T>($"Default-{typeof(T).Name}") ??
                      sp.GetService<T>();
        }

        return service;
    }

    /// <summary>
    /// Registers MCP plugin services with the host application builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The updated host application builder.</returns>
    public static IHostApplicationBuilder AddMcpPluginServices(this IHostApplicationBuilder builder)
    {
        // Register the MCP plugin container as a singleton
        builder.Services.TryAddSingleton<MCPServerContainer>();

        // Register the MCP plugin manager as a singleton with factory pattern
        builder.Services.TryAddSingleton<McpPluginManager>((serviceProvider) =>
        {
            var pluginContainer = serviceProvider.GetRequiredService<MCPServerContainer>();
            var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

            var logger = loggerFactory?.CreateLogger<McpPluginManager>();
            return new McpPluginManager(serviceScopeFactory, pluginContainer, logger);
        });

        // Register the MCP plugin background service
        builder.Services.AddHostedService<McpPluginBackgroundService>();

        return builder;
    }
}
