using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Helpers;

namespace ProjectVico.V2.Shared.Extensions;

public static class KernelMemoryExtensions
{

    private const int PartitionSize = 1200;
    private const int MaxTokensPerLine = 100;

    public static IHostApplicationBuilder AddKeyedKernelMemoryForDocumentProcess(
        this IHostApplicationBuilder builder, 
        ServiceConfigurationOptions serviceConfigurationOptions, 
        DocumentProcessOptions documentProcessOptions, 
        string key = null)
    {
        var sp = builder.Services.BuildServiceProvider();
        var openAi = sp.GetKeyedService<OpenAIClient>("openai-planner");
        var azureCredentialHelper = sp.GetRequiredService<AzureCredentialHelper>();

        var baseSearchClient = sp.GetService<SearchIndexClient>();

        var configuration = builder.Configuration;

        if (string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(documentProcessOptions.Name))
        {
            key = documentProcessOptions.Name;
        }

        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key must be provided or Document Process must have a valid name");
        }

        var openAiConnString = builder.Configuration.GetConnectionString("openai-planner");
        
        // Get Endpoint from Connection String
        var openAiEndpoint = openAiConnString!.Split(";").FirstOrDefault(x => x.Contains("Endpoint="))?.Split("=")[1];
        // Get Key from Connection String
        var openAiKey = openAiConnString!.Split(";").FirstOrDefault(x => x.Contains("Key="))?.Split("=")[1];

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
        var blobConnection = configuration.GetConnectionString("blob-docing");

        if (string.IsNullOrEmpty(blobConnection))
        {
            throw new ArgumentException("Blob Connection String must be provided");
        }

        // The connection string is just a URL. The account name is the first part after https:// and before the first period.
        var blobAccountName = blobConnection.Split(".")[0].Split("//")[1];
        var documentProcessContainerName = documentProcessOptions.Name.ToLower().Replace(" ", "-").Replace(".", "-")+"-km-blobs";

        var azureBlobsConfig = new AzureBlobsConfig()
        {
            Account = blobAccountName,
            ConnectionString = blobConnection,
            Container = documentProcessContainerName,
            Auth = AzureBlobsConfig.AuthTypes.ManualTokenCredential
        };

        azureBlobsConfig.SetCredential(azureCredentialHelper.GetAzureCredential());
        
        var textPartitioningOptions = new TextPartitioningOptions
        {
            MaxTokensPerParagraph = PartitionSize,
            MaxTokensPerLine = MaxTokensPerLine,
            OverlappingTokens = 0
        };

        KernelMemoryConfig? kernelMemoryConfig = null;
        // Only do this if no KernelMemoryConfig has been registered in the service collection
        if (sp.GetService<KernelMemoryConfig>() == null)
        {
            builder.Services.AddOptions<KernelMemoryConfig>().Bind(builder.Configuration.GetSection("KernelMemory"));
            kernelMemoryConfig = builder.Configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()! ?? new KernelMemoryConfig();
            kernelMemoryConfig.DataIngestion ??= new KernelMemoryConfig.DataIngestionConfig();

            if (kernelMemoryConfig.DataIngestion.MemoryDbUpsertBatchSize < 20)
            {
                kernelMemoryConfig.DataIngestion.MemoryDbUpsertBatchSize = 20;
            }

            builder.Services.AddSingleton<KernelMemoryConfig>(kernelMemoryConfig);
        }

        var kernelMemoryBuilder = new KernelMemoryBuilder();
        if (kernelMemoryConfig != null)
        {
            kernelMemoryBuilder.Services.AddSingleton(kernelMemoryConfig);
        }

        kernelMemoryBuilder
            .WithAzureOpenAITextEmbeddingGeneration(openAiEmbeddingConfig)
            .WithAzureOpenAITextGeneration(openAiChatCompletionConfig)
            .WithCustomTextPartitioningOptions(textPartitioningOptions)
            .WithAzureBlobsDocumentStorage(azureBlobsConfig)
            .WithAzureAISearchMemoryDb(azureAiSearchConfig);
        
        // Add Logging
        kernelMemoryBuilder.Services.AddLogging(l =>
        {
            l.AddConsole().SetMinimumLevel(LogLevel.Information);
            l.AddConfiguration(builder.Configuration);
        });
       
        var kernelMemory = kernelMemoryBuilder.Build();

        builder.Services.AddKeyedSingleton<IKernelMemory>(documentProcessOptions.Name+"-IKernelMemory", kernelMemory);
        
        return builder;
    }
}