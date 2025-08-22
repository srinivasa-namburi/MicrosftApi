// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Extensions;
using Xunit;
using Moq;
using Azure.Search.Documents.Indexes;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Extensions
{
    public class KernelMemoryExtensionsTests
    {
        private readonly Mock<SearchIndexClient> _mockSearchIndexClient;
        private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

        public KernelMemoryExtensionsTests()
        {
            _mockSearchIndexClient = new Mock<SearchIndexClient>();
            _mockSearchIndexClient.Setup(x => x.Endpoint).Returns(new Uri("https://test.search.windows.net"));

            _serviceConfigurationOptions = new ServiceConfigurationOptions
            {
                OpenAi = new ServiceConfigurationOptions.OpenAiOptions
                {
                    EmbeddingModelDeploymentName = "test_embedding",
                    GPT4oModelDeploymentName = "test_gpt4o"
                },
                GreenlightServices = new ServiceConfigurationOptions.GreenlightServicesOptions
                {
                    Global = new ServiceConfigurationOptions.GreenlightServicesOptions.GlobalOptions
                    {
                        UsePostgresMemory = false
                    }
                }
            };
        }

        [Fact]
        public void GetKernelMemoryInstanceForDocumentLibrary_WhenConnectionStringIsEmpty_ShouldThrowException()
        {
            // Arrange
            var documentLibraryInfo = new DocumentLibraryInfo { ShortName = "TestLibrary" };
            IConfiguration configuration = new ConfigurationBuilder().Build();
            var azureCredentialHelper = new AzureCredentialHelper(configuration);

            var serviceCollection = new ServiceCollection()
                .AddSingleton(configuration)
                .AddSingleton(azureCredentialHelper)
                .AddSingleton(_mockSearchIndexClient.Object);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                serviceProvider.GetKernelMemoryInstanceForDocumentLibrary(_serviceConfigurationOptions, documentLibraryInfo);
            });
        }

        [Fact]
        public void GetKernelMemoryInstanceForDocumentLibrary_WhenMissingOpenAIEndpoint_ShouldThrowException()
        {
            // Arrange
            var documentLibraryInfo = new DocumentLibraryInfo { ShortName = "TestLibrary" };

            var inMemoryConfiguration = new Dictionary<string, string?>
            {
                { "ConnectionStrings:openai-planner", "" },
                { "ConnectionStrings:kmvectordb", "Host=localhost;Port=9002;Username=postgres;Password=postgres;Database=kmvectordb "}
            };

            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfiguration).Build();
            var azureCredentialHelper = new AzureCredentialHelper(configuration);

            var serviceCollection = new ServiceCollection()
                .AddSingleton(configuration)
                .AddSingleton(azureCredentialHelper)
                .AddSingleton(_mockSearchIndexClient.Object);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                serviceProvider.GetKernelMemoryInstanceForDocumentLibrary(_serviceConfigurationOptions, documentLibraryInfo);
            });
        }

        [Fact]
        public void GetKernelMemoryInstanceForDocumentLibrary_WhenMissingOpenAIKey_ShouldThrowException()
        {
            // Arrange
            var documentLibraryInfo = new DocumentLibraryInfo { ShortName = "TestLibrary" };

            var inMemoryConfiguration = new Dictionary<string, string?>
            {
                { "ConnectionStrings:openai-planner", "Endpoint=test" },
                { "ConnectionStrings:kmvectordb", "Host=localhost;Port=9002;Username=postgres;Password=postgres;Database=kmvectordb "}
            };

            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfiguration).Build();
            var azureCredentialHelper = new AzureCredentialHelper(configuration);

            var serviceCollection = new ServiceCollection()
                .AddSingleton(configuration)
                .AddSingleton(azureCredentialHelper)
                .AddSingleton(_mockSearchIndexClient.Object);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                serviceProvider.GetKernelMemoryInstanceForDocumentLibrary(_serviceConfigurationOptions, documentLibraryInfo);
            });
        }

        [Fact]
        public void GetKernelMemoryInstanceForDocumentLibrary_WhenMissingBlobConnectionString_ShouldThrowException()
        {
            // Arrange
            var documentLibraryInfo = new DocumentLibraryInfo { ShortName = "TestLibrary" };

            var inMemoryConfiguration = new Dictionary<string, string?>
            {
                { "ConnectionStrings:openai-planner", "Endpoint=test;Key=test" },
                { "ConnectionStrings:kmvectordb", "Host=localhost;Port=9002;Username=postgres;Password=postgres;Database=kmvectordb "}
            };

            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfiguration).Build();
            var azureCredentialHelper = new AzureCredentialHelper(configuration);

            var serviceCollection = new ServiceCollection()
                .AddSingleton(configuration)
                .AddSingleton(azureCredentialHelper)
                .AddSingleton(_mockSearchIndexClient.Object);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                serviceProvider.GetKernelMemoryInstanceForDocumentLibrary(_serviceConfigurationOptions, documentLibraryInfo);
            });
        }

        [Fact]
        public void GetKernelMemoryInstanceForDocumentLibrary_WhenConfiguredCorrectly_ShouldCreateKernelMemoryInstance()
        {
            // Arrange
            var documentLibraryInfo = new DocumentLibraryInfo { ShortName = "TestLibrary" };

            var inMemoryConfiguration = new Dictionary<string, string?>
            {
                { "ConnectionStrings:openai-planner", "Endpoint=https://test.com;Key=test" },
                { "ConnectionStrings:blob-docing", "DefaultEndpointsProtocol=https;AccountName=http://test.azure.com;AccountKey=test" },
                { "ConnectionStrings:kmvectordb", "Host=localhost;Port=9002;Username=postgres;Password=postgres;Database=kmvectordb "}
            };

            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfiguration).Build();
            var azureCredentialHelper = new AzureCredentialHelper(configuration);

            var serviceCollection = new ServiceCollection()
                .AddSingleton(configuration)
                .AddSingleton(azureCredentialHelper)
                .AddSingleton(_mockSearchIndexClient.Object);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act
            var result = serviceProvider.GetKernelMemoryInstanceForDocumentLibrary(_serviceConfigurationOptions, documentLibraryInfo);

            // Assert
            Assert.NotNull(result);
        }
    }
}
