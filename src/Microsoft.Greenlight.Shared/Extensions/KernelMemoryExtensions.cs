using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Extensions;

public static class KernelMemoryExtensions
{
    private const int PartitionSize = 1200;
    private const int MaxTokensPerLine = 100;

    public static IHostApplicationBuilder AddKeyedKernelMemoryForDocumentProcess(
        this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions,
        DocumentProcessOptions documentProcessOptions,
        string? key = null)
    {
        var documentProcessName = documentProcessOptions.Name;
        return AddKeyedKernelMemoryForDocumentProcess(builder, serviceConfigurationOptions, documentProcessName, key);
    }

    public static IHostApplicationBuilder AddKeyedKernelMemoryForDocumentProcess(
        this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions,
        DocumentProcessInfo documentProcessInfo,
        string? key = null
    )
    {
        var documentProcessName = documentProcessInfo.ShortName;
        return AddKeyedKernelMemoryForDocumentProcess(builder, serviceConfigurationOptions, documentProcessName, key);
    }

    public static IHostApplicationBuilder AddKeyedKernelMemoryForDocumentProcess(
        this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions,
        string documentProcessName = "",
        string? key = "")
    {
        var serviceProvider = builder.Services.BuildServiceProvider();
        var blobContainerName = documentProcessName.ToLower().Replace(" ", "-").Replace(".", "-") + "-km-blobs";

        var kernelMemory = CreateKernelMemoryInstance(
            serviceProvider,
            serviceConfigurationOptions,
            documentProcessName,
            blobContainerName
            );

        builder.Services.AddKeyedSingleton<IKernelMemory>(documentProcessName + "-IKernelMemory", kernelMemory);

        return builder;
    }

    public static IHostApplicationBuilder AddKernelMemoryForReviews(
        this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions
    )
    {
        builder.AddKeyedKernelMemoryForDocumentProcess(serviceConfigurationOptions, "Reviews");
        return builder;
    }

    public static IKernelMemory GetKernelMemoryInstanceForDocumentLibrary(
        this IServiceProvider serviceProvider,
        ServiceConfigurationOptions serviceConfigurationOptions,
        DocumentLibraryInfo documentLibraryInfo,
        string? key = null)
    {
        var documentLibraryShortName = documentLibraryInfo.ShortName;
        var indexName = documentLibraryInfo.IndexName ?? throw new ArgumentException("IndexName must be provided in DocumentLibraryInfo");

        var blobContainerName =  documentLibraryShortName.ToLower().Replace(" ", "-").Replace(".", "-") + "-km-blobs";

        var kernelMemory = CreateKernelMemoryInstance(
            serviceProvider,
            serviceConfigurationOptions,
            documentLibraryShortName,
            blobContainerName,
            indexName
           );

        return kernelMemory;
    }


    private static IKernelMemory CreateKernelMemoryInstance(
        IServiceProvider serviceProvider,
        ServiceConfigurationOptions serviceConfigurationOptions,
        string documentLibraryName,
        string blobContainerName,
        string indexName = ""
    )
    {
        var azureCredentialHelper = serviceProvider.GetRequiredService<AzureCredentialHelper>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var baseSearchClient = serviceProvider.GetService<SearchIndexClient>();

        // Get OpenAI connection string
        var openAiConnString = configuration.GetConnectionString("openai-planner") ?? throw new InvalidOperationException("OpenAI connection string not found.");

        // Extract Endpoint and Key
        var openAiEndpoint = openAiConnString.Split(";").FirstOrDefault(x => x.Contains("Endpoint="))?.Split("=")[1];
        var openAiKey = openAiConnString.Split(";").FirstOrDefault(x => x.Contains("Key="))?.Split("=")[1];

        var openAiEmbeddingConfig = new AzureOpenAIConfig()
        {
            Auth = AzureOpenAIConfig.AuthTypes.APIKey,
            Endpoint = openAiEndpoint,
            APIKey = openAiKey,
            Deployment = serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName,
            APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration
        };

        var openAiChatCompletionConfig = new AzureOpenAIConfig()
        {
            Auth = AzureOpenAIConfig.AuthTypes.APIKey,
            Endpoint = openAiEndpoint,
            APIKey = openAiKey,
            Deployment = serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName,
            APIType = AzureOpenAIConfig.APITypes.ChatCompletion
        };

        var azureAiSearchConfig = new AzureAISearchConfig()
        {
            Endpoint = baseSearchClient.Endpoint.AbsoluteUri,
            Auth = AzureAISearchConfig.AuthTypes.ManualTokenCredential
        };

        azureAiSearchConfig.SetCredential(azureCredentialHelper.GetAzureCredential());

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
            MaxTokensPerLine = MaxTokensPerLine,
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
            .WithAzureBlobsDocumentStorage(azureBlobsConfig)
            .WithAzureAISearchMemoryDb(azureAiSearchConfig)
            ;

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
