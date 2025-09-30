using Azure.Core;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Extensions;

/// <summary>
/// Provides extension methods for configuring Kernel Memory in the application.
/// </summary>
public static class KernelMemoryExtensions
{
    private const int PartitionSize = 1200;

    /// <summary>
    /// Adds keyed Kernel Memory for document processing given a document process name.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <param name="documentProcessName">The document process name.</param>
    /// <param name="key">The optional key.</param>
    /// <returns>The updated host application builder.</returns>
    public static IHostApplicationBuilder AddKeyedKernelMemoryForDocumentProcess(
        this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions,
        string documentProcessName = "",
        string? key = "")
    {
        // Register a factory that creates KernelMemory instances when needed
        // instead of using a temporary service provider
        builder.Services.AddKeyedSingleton<IKernelMemory>(documentProcessName + "-IKernelMemory", (sp, _) => 
        {
            var blobContainerName = documentProcessName.ToLower().Replace(" ", "-").Replace(".", "-") + "-km-blobs";
            return CreateKernelMemoryInstance(
                sp,
                serviceConfigurationOptions,
                blobContainerName
            );
        });

        return builder;
    }

    /// <summary>
    /// Adds Kernel Memory for reviews.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <returns>The updated host application builder.</returns>
    public static IHostApplicationBuilder AddKernelMemoryForReviews(
        this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions)
    {
        builder.AddKeyedKernelMemoryForDocumentProcess(serviceConfigurationOptions, "Reviews");
        return builder;
    }

    /// <summary>
    /// Gets the Kernel Memory instance for the document library.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <param name="documentLibraryInfo">The document library information.</param>
    /// <param name="key">The optional key.</param>
    /// <returns>The Kernel Memory instance.</returns>
    public static IKernelMemory GetKernelMemoryInstanceForDocumentLibrary(
        this IServiceProvider serviceProvider,
        ServiceConfigurationOptions serviceConfigurationOptions,
        DocumentLibraryInfo documentLibraryInfo,
        string? key = null)
    {
        // Create a new scope to ensure services aren't disposed
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        
        var documentLibraryShortName = documentLibraryInfo.ShortName;
        var indexName = documentLibraryInfo.IndexName;

        var blobContainerName = documentLibraryShortName.ToLower().Replace(" ", "-").Replace(".", "-") + "-km-blobs";

        var kernelMemory = CreateKernelMemoryInstance(
            scopedProvider,
            serviceConfigurationOptions,
            blobContainerName,
            indexName
           );

        return kernelMemory;
    }

    /// <summary>
    /// Creates a Kernel Memory instance for ad-hoc uploads.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="serviceConfigurationOptions">The service configuration options</param>
    /// <returns></returns>
    public static IKernelMemory GetKernelMemoryForAdHocUploads(
        this IServiceProvider serviceProvider,
        ServiceConfigurationOptions serviceConfigurationOptions)
    {
        // Create a new scope to ensure services aren't disposed
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        
        const string blobContainerName = "adhoc-km-blobs";
        const string indexName = "index-adhoc-km";

        var kernelMemory = CreateKernelMemoryInstance(
            scopedProvider,
            serviceConfigurationOptions,
            blobContainerName,
            indexName
            );

        return kernelMemory;
    }

    private static IKernelMemory CreateKernelMemoryInstance(
        IServiceProvider serviceProvider,
        ServiceConfigurationOptions serviceConfigurationOptions,
        string blobContainerName,
        string indexName = "")
    {
        var azureCredentialHelper = serviceProvider.GetRequiredService<AzureCredentialHelper>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var baseSearchClient = serviceProvider.GetService<SearchIndexClient>(); // Optional when using Postgres

        // Get OpenAI connection string - optional for system startup
        var openAiConnString = configuration.GetConnectionString("openai-planner");
        if (string.IsNullOrEmpty(openAiConnString))
        {
            var logger = serviceProvider.GetService<ILogger<IKernelMemory>>();
            logger?.LogWarning("OpenAI connection string not configured - Kernel Memory features will be unavailable until configured.");
            // Return null - callers should handle this gracefully
            return null!;
        }
        var kmVectorDbConnectionString = configuration.GetConnectionString("kmvectordb");

        // Parse OpenAI connection string using helper
        var connectionInfo = AzureOpenAIConnectionStringParser.Parse(openAiConnString);
        var openAiEndpoint = connectionInfo.Endpoint;
        var openAiKey = connectionInfo.Key;

        AzureOpenAIConfig.AuthTypes authType;
        string? apiKey = null;
        TokenCredential? tokenCredential = null;

        if (!string.IsNullOrEmpty(openAiKey))
        {
            authType = AzureOpenAIConfig.AuthTypes.APIKey;
            apiKey = openAiKey;
        }
        else
        {
            authType = AzureOpenAIConfig.AuthTypes.ManualTokenCredential;
            tokenCredential = azureCredentialHelper.GetAzureCredential();
        }

        var openAiEmbeddingConfig = new AzureOpenAIConfig()
        {
            Auth = authType,
            Endpoint = openAiEndpoint,
            Deployment = serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName,
            APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration
        };

        // Set the token credential if using manual token credential authentication, if not set the API key
        if (authType == AzureOpenAIConfig.AuthTypes.APIKey && apiKey != null)
        {
            openAiEmbeddingConfig.APIKey = apiKey;
        }
        else if (tokenCredential != null)
        {
            openAiEmbeddingConfig.SetCredential(tokenCredential);
        }

        var openAiChatCompletionConfig = new AzureOpenAIConfig()
        {
            Auth = authType,
            Endpoint = openAiEndpoint,
            Deployment = serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName,
            APIType = AzureOpenAIConfig.APITypes.ChatCompletion
        };

        // Set the token credential if using manual token credential authentication, if not set the API key
        if (authType == AzureOpenAIConfig.AuthTypes.APIKey && apiKey != null)
        {
            openAiChatCompletionConfig.APIKey = apiKey;
        }
        else if (tokenCredential != null)
        {
            openAiChatCompletionConfig.SetCredential(tokenCredential);
        }

        AzureAISearchConfig? azureAiSearchConfig = null;
        if (baseSearchClient != null)
        {
            azureAiSearchConfig = new AzureAISearchConfig()
            {
                Endpoint = baseSearchClient.Endpoint.AbsoluteUri,
                Auth = AzureAISearchConfig.AuthTypes.ManualTokenCredential
            };
        }

        PostgresConfig? postgresConfig = null;
        if (kmVectorDbConnectionString != null)
        {
            postgresConfig = new PostgresConfig
            {
                ConnectionString = kmVectorDbConnectionString,
                Schema = "km",
                TableNamePrefix = "km_",
                Columns = new Dictionary<string, string>
                {
                    { "id", "_pk" },
                    { "embedding", "embedding" },
                    { "tags", "labels" },
                    { "content", "chunk" },
                    { "payload", "extras" }
                },
                CreateTableSql =
                [
                    "BEGIN;                                                                      ",
                    "SELECT pg_advisory_xact_lock(%%lock_id%%);                                  ",
                    "CREATE TABLE IF NOT EXISTS %%table_name%% (                                 ",
                    "  _pk         TEXT NOT NULL PRIMARY KEY,                                    ",
                    "  embedding   vector(%%vector_size%%),                                      ",
                    "  labels      TEXT[] DEFAULT '{}'::TEXT[] NOT NULL,                         ",
                    "  chunk       TEXT DEFAULT '' NOT NULL,                                     ",
                    "  extras      JSONB DEFAULT '{}'::JSONB NOT NULL,                           ",
                    "  my_field1   TEXT DEFAULT '',                                              ",
                    "  _update     TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP            ",
                    ");                                                                          ",
                    "CREATE INDEX ON %%table_name%% USING GIN(labels);                           ",
                    "CREATE INDEX ON %%table_name%% USING hnsw (embedding vector_cosine_ops); ",
                    "COMMIT;                                                                     "
                ]
            };
        }


        azureAiSearchConfig?.SetCredential(azureCredentialHelper.GetAzureCredential());

        // Blob Connection String
        var blobConnection = configuration.GetConnectionString("blob-docing") ?? throw new ArgumentException("Blob Connection String must be provided");

        // Extract Blob account name
        var blobAccountName = blobConnection.Split(".")[0].Split("//")[1];

        var azureBlobsConfig = new AzureBlobsConfig()
        {
            Account = blobAccountName,
            ConnectionString = blobConnection,
            Container = blobContainerName,
            Auth = AzureBlobsConfig.AuthTypes.ManualTokenCredential
        };

        azureBlobsConfig.SetCredential(azureCredentialHelper.GetAzureCredential());

        var textPartitioningOptions = new TextPartitioningOptions
        {
            MaxTokensPerParagraph = PartitionSize,
            OverlappingTokens = 0
        };

        // Get or create KernelMemoryConfig
        var kernelMemoryConfig = serviceProvider.GetService<KernelMemoryConfig>() ?? configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>() ?? new KernelMemoryConfig();

        if (kernelMemoryConfig.DataIngestion.MemoryDbUpsertBatchSize < 20)
        {
            kernelMemoryConfig.DataIngestion.MemoryDbUpsertBatchSize = 20;
        }

        if (!string.IsNullOrEmpty(indexName))
        {
            kernelMemoryConfig.DefaultIndexName = indexName;
        }

        var kernelMemoryBuilder = new KernelMemoryBuilder();

        kernelMemoryBuilder.Services.AddSingleton(kernelMemoryConfig);

        kernelMemoryBuilder
            .WithAzureOpenAITextEmbeddingGeneration(openAiEmbeddingConfig)
            .WithAzureOpenAITextGeneration(openAiChatCompletionConfig)
            .WithCustomTextPartitioningOptions(textPartitioningOptions)
            .WithAzureBlobsDocumentStorage(azureBlobsConfig);

        if (serviceConfigurationOptions.GreenlightServices.Global.UsePostgresMemory && postgresConfig != null)
        {
            kernelMemoryBuilder.WithPostgresMemoryDb(postgresConfig);
        }
        else if (azureAiSearchConfig != null)
        {
            kernelMemoryBuilder.WithAzureAISearchMemoryDb(azureAiSearchConfig);
        }
        else
        {
            throw new InvalidOperationException("Neither Postgres nor Azure AI Search is properly configured for Kernel Memory vector storage.");
        }

        // Add Logging
        kernelMemoryBuilder.Services.AddLogging(l =>
        {
            l.AddConsole().SetMinimumLevel(LogLevel.Information);
            l.AddConfiguration(configuration);
        });

        var kernelMemory = kernelMemoryBuilder.Build();

        return kernelMemory;
    }
}