using System.ClientModel;
using Aspire.Azure.Messaging.ServiceBus;
using Aspire.Azure.Search.Documents;
using Aspire.Azure.Storage.Blobs;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Exporters;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management.Configuration;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Plugins;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Validation;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Embeddings;
using StackExchange.Redis;
using StackExchange.Redis.Configuration;
using System.Reflection;
using Microsoft.Extensions.Options;

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
        var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

        // Add the database context
        builder.AddDocGenDbContext(serviceConfigurationOptions);

        // Skip database configuration provider setup if running in "Microsoft.Greenlight.SetupManager.DB"
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (entryAssemblyName != "Microsoft.Greenlight.SetupManager.DB")
        {
            // Create the source first
            var configSource = new EfCoreConfigurationProviderSource(builder.Services);

            // Add the database configuration provider
            builder.Services.AddSingleton<EfCoreConfigurationProvider>(sp =>
            {
                var dbContext = sp.GetRequiredService<DocGenerationDbContext>();
                var logger = sp.GetRequiredService<ILogger<EfCoreConfigurationProvider>>();
                var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ServiceConfigurationOptions>>();
                var configuration = (IConfigurationRoot)sp.GetRequiredService<IConfiguration>();

                var provider = new EfCoreConfigurationProvider(dbContext, logger, optionsMonitor, configuration);

                // Store the reference to this instance in the source
                configSource.SetProviderInstance(provider);

                return provider;
            });

            // Add the EfCoreConfigurationProvider to the configuration sources
            builder.Configuration.Add(configSource);
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
    public static IHostApplicationBuilder AddGreenlightServices(this IHostApplicationBuilder builder, AzureCredentialHelper credentialHelper, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        builder.Services.AddAutoMapper(typeof(DocumentProcessInfoProfile));

        // Common services and dependencies
        builder.AddAzureServiceBusClient("sbus", configureSettings: delegate (AzureMessagingServiceBusSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddAzureSearchClient("aiSearch", configureSettings: delegate (AzureSearchSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddAzureBlobClient("blob-docing", configureSettings: delegate (AzureStorageBlobsSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        // These services use key-based authentication and don't need to use the AzureCredentialHelper.
        // builder.AddKeyedAzureOpenAIClient("openai-planner");

        builder.Services.AddKeyedSingleton<AzureOpenAIClient>("openai-planner", (sp, obj) =>
        {
            // Here's the connection string format. Parse this for endpoint and key
            // Endpoint=https://pvico-oai-swedencentral.openai.azure.com/;Key=18daceb070ee44eea06a277ff0454492

            var connectionString = builder.Configuration.GetConnectionString("openai-planner");
            var endpoint = connectionString!.Split(';')[0].Split('=')[1];
            var key = connectionString!.Split(';')[1].Split('=')[1];

            var openAIClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
            return openAIClient;
        });

        builder.Services.AddScoped<IChatCompletionService>(service =>
            new AzureOpenAIChatCompletionService(serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName,
                service.GetRequiredKeyedService<AzureOpenAIClient>("openai-planner"), "openai-chatcompletion")
        );


        builder.Services.AddScoped<ITextEmbeddingGenerationService>(service =>
            new AzureOpenAITextEmbeddingGenerationService(serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName,
                service.GetRequiredKeyedService<AzureOpenAIClient>("openai-planner"), "openai-embeddinggeneration")
        );

        builder.AddGreenLightRedisClient("redis", credentialHelper, serviceConfigurationOptions);

        builder.Services.AddScoped<AzureFileHelper>();
        builder.Services.AddSingleton<SearchClientFactory>();

        builder.Services.AddKeyedTransient<IDocumentExporter, WordDocumentExporter>("IDocumentExporter-Word");

        builder.Services.AddSingleton<IPluginSourceReferenceCollector, PluginSourceReferenceCollector>();
        builder.Services.AddKeyedScoped<IFunctionInvocationFilter, InputOutputTrackingPluginInvocationFilter>("InputOutputTrackingPluginInvocationFilter");

        builder.Services.AddSingleton<ValidationStepExecutionLogicFactory>();

        // Add all IValidationStepExecutionLogic implementations
        builder.Services
            .AddKeyedScoped<IValidationStepExecutionLogic, ParallelByOuterChapterValidationStepExecutionLogic>(
                nameof(ParallelByOuterChapterValidationStepExecutionLogic));

        builder.Services
            .AddKeyedScoped<IValidationStepExecutionLogic, ParallelFullDocumentValidationStepExecutionLogic>(
                nameof(ParallelFullDocumentValidationStepExecutionLogic));

        builder.Services
            .AddKeyedScoped<IValidationStepExecutionLogic, SequentialFullDocumentValidationStepExecutionLogic>(
                nameof(SequentialFullDocumentValidationStepExecutionLogic));

        builder.Services.AddScoped<IContentNodeService, ContentNodeService>();

        return builder;
    }

    /// <summary>
    /// Adds a Redis client to the host application builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="redisConnectionStringName">The name of the Redis connection string.</param>
    /// <param name="credentialHelper">The Azure credential helper.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <returns>The updated host application builder.</returns>
    private static IHostApplicationBuilder AddGreenLightRedisClient(this IHostApplicationBuilder builder,
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
        }
        else
        {
            builder.AddRedisClient(redisConnectionStringName);
        }

        return builder;
    }


    /// <summary>
    /// Gets a service for the specified document process.
    /// </summary>
    /// <typeparam name="T">The type of the service to get.</typeparam>
    /// <param name="sp">The service provider.</param>
    /// <param name="documentProcessInfo">The document process information.</param>
    /// <returns>The service instance if found; otherwise, null.</returns>
    public static T? GetServiceForDocumentProcess<T>(this IServiceProvider sp, DocumentProcessInfo documentProcessInfo)
    {
        var service = sp.GetServiceForDocumentProcess<T>(documentProcessInfo.ShortName);
        return service;
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
    public static T GetRequiredServiceForDocumentProcess<T>(this IServiceProvider sp, string documentProcessName)
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
    public static T? GetServiceForDocumentProcess<T>(this IServiceProvider sp, string documentProcessName)
    {
        T? service = default;

        // Don't use using here as we are retrieving a service to be used outside of the scope of the current request.
        var scope = sp.CreateScope();

        var documentProcessInfoService = scope.ServiceProvider.GetRequiredService<IDocumentProcessInfoService>();
        var documentProcessInfo = documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName).Result;

        if (documentProcessInfo == null && documentProcessName != "Reviews")
        {
            throw new InvalidOperationException($"Document process info not found for {documentProcessName}");
        }

        var dynamicServiceKey = $"Dynamic-{typeof(T).Name}";
        var documentProcessServiceKey = $"{documentProcessName}-{typeof(T).Name}";

        // Try to get a scoped service for the specific document process, then the dynamic service, then the default service, then finally a service with no key.
        // This allows for a service to be registered for a specific document process, or a default service to be registered for all document processes,
        // or a service to be registered with no key for use outside of the document process context.
        service = scope.ServiceProvider.GetKeyedService<T>(documentProcessServiceKey) ??
                  scope.ServiceProvider.GetKeyedService<T>(dynamicServiceKey) ??
                  scope.ServiceProvider.GetKeyedService<T>($"Default-{typeof(T).Name}") ??
                  scope.ServiceProvider.GetService<T>();

        // If the service is still null - it may not be scoped but exists as a singleton. Try to get the singleton service.
        if (service == null)
        {
            service = sp.GetKeyedService<T>(documentProcessServiceKey) ??
                      sp.GetKeyedService<T>(dynamicServiceKey) ??
                      sp.GetKeyedService<T>($"Default-{typeof(T).Name}") ??
                      sp.GetService<T>();
        }

        return service;
    }

    /// <summary>
    /// Get a Semantic Kernel instance specifically for document validation for a given document process.
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="documentProcessName"></param>
    /// <returns></returns>
    public static Kernel? GetValidationSemanticKernelForDocumentProcess(this IServiceProvider sp, string documentProcessName)
    {
        if (sp == null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        if (documentProcessName == null)
        {
            throw new ArgumentNullException(nameof(documentProcessName));
        }

        var kernelFactory = sp.GetRequiredService<IKernelFactory>();
        var kernel = kernelFactory.GetValidationKernelForDocumentProcessAsync(documentProcessName).Result;
        
        return kernel;
    }
}
