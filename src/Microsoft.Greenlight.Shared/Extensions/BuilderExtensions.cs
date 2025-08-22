using Aspire.Azure.Messaging.ServiceBus;
using Aspire.Azure.Search.Documents;
using Aspire.Azure.Storage.Blobs;
using AutoMapper;
using Azure.AI.OpenAI;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage;
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
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion; // legacy SK chat service still used in some factories
using Microsoft.SemanticKernel.Connectors.AzureOpenAI; // legacy SK connectors
using Microsoft.SemanticKernel.Embeddings; // legacy SK embeddings (to be removed later)
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Azure;
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

        // Common services and dependencies
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


        builder.Services.AddKeyedSingleton<AzureOpenAIClient>("openai-planner", (sp, obj) =>
            {
                // Here's the connection string format. Parse this for endpoint and key
                // Endpoint=https://pvico-oai-swedencentral.openai.azure.com/;Key=sdfsdfsdfsdfsdfsdfsdf

                var connectionString = builder.Configuration.GetConnectionString("openai-planner");
                var endpoint = connectionString!.Split(';')[0].Split('=')[1];
                var key = connectionString!.Split(';')[1].Split('=')[1];

                // If the connection string doesn't contain a Key, use credentialHelper.GetAzureCredential() method.
                // Otherwise, use the key with an ApiKeyCredential.
                return string.IsNullOrEmpty(key)
                    ? new AzureOpenAIClient(new Uri(endpoint), credentialHelper.GetAzureCredential(), new AzureOpenAIClientOptions()
                    {
                        NetworkTimeout = 30.Minutes()
                    })
                    : new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key), new AzureOpenAIClientOptions()
                    {
                        NetworkTimeout = 30.Minutes()
                    });
            });

        // NOTE: Microsoft.Extensions.AI OpenAI convenience adapters not available in current package version.
        // Keep legacy SK chat service registration inside SemanticKernelFactory until upgraded.


        builder.Services.AddTransient<DynamicDocumentProcessServiceFactory>();

        builder.Services.AddTransient<AzureFileHelper>();
        builder.Services.AddSingleton<SearchClientFactory>();

        builder.Services.AddTransient<IDocumentProcessInfoService, DocumentProcessInfoService>();
        builder.Services.AddTransient<IPromptInfoService, PromptInfoService>();
        builder.Services.AddTransient<IDocumentLibraryInfoService, DocumentLibraryInfoService>();

        // Register the Kernel Memory Instance Factory for Document Libraries and text extraction
        builder.Services.AddSingleton<KernelMemoryInstanceContainer>();
        // This can be scoped because it relies on the singleton KernelMemoryInstanceContainer to keep track of the instances
        builder.Services.AddTransient<IKernelMemoryInstanceFactory, KernelMemoryInstanceFactory>();

        // Text extraction service for content references using kernel memory
        builder.Services.AddTransient<IKernelMemoryTextExtractionService, KernelMemoryTextExtractionService>();

        builder.Services.AddKeyedTransient<IDocumentExporter, WordDocumentExporter>("IDocumentExporter-Word");

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

        // Register primary content reference service
        builder.Services.AddTransient<IContentReferenceService, ContentReferenceService>();

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

        // Context Builder that uses embeddings generation to build rag contexts from content references for user queries
        builder.Services.AddTransient<IRagContextBuilder, RagContextBuilder>();

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

            // Add EventHub-based streaming for high throughput
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

        builder.UseOrleansClient(siloBuilder =>
        {

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
