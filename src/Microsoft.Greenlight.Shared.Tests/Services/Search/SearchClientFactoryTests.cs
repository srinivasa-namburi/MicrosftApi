using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services.Search;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Services.Search
{
    public class SearchClientFactoryTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<SearchIndexClient> _mockBaseSearchIndexClient;
        private readonly Mock<AzureCredentialHelper> _mockAzureCredentialHelper;
        private readonly Mock<IOptions<ServiceConfigurationOptions>> _mockServiceConfigurationOptions;
        private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
        private readonly SearchClientFactory _factory;

        public SearchClientFactoryTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockBaseSearchIndexClient = new Mock<SearchIndexClient>(new Uri("https://example.com"),new DefaultAzureCredential());
            _mockAzureCredentialHelper = new Mock<AzureCredentialHelper>(_mockConfiguration.Object);
            _serviceConfigurationOptions = new ServiceConfigurationOptions();
            _mockServiceConfigurationOptions = new Mock<IOptions<ServiceConfigurationOptions>>();
            _mockServiceConfigurationOptions.Setup(x => x.Value).Returns(_serviceConfigurationOptions);
            _mockBaseSearchIndexClient.Setup(x => x.Endpoint).Returns(new System.Uri("https://example.com"));

            _factory = new SearchClientFactory(
                _mockConfiguration.Object,
                _mockBaseSearchIndexClient.Object,
                _mockServiceConfigurationOptions.Object,
                _mockAzureCredentialHelper.Object
            );
        }

        [Fact]
        public void GetSearchIndexClientForIndex_WhenClientNotInitialized_ReturnsSearchIndexClient()
        {
            // Arrange
            var indexName = "test-index";

            // Act
            var result = _factory.GetSearchIndexClientForIndex(indexName);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SearchIndexClient>(result);
        }

        [Fact]
        public void GetSearchIndexClientForIndex_WhenClientIsInitialized_ReturnsSameSearchIndexClient()
        {
            // Arrange
            var indexName = "test-index";

            // Act
            var initialClient = _factory.GetSearchIndexClientForIndex(indexName);
            var result = _factory.GetSearchIndexClientForIndex(indexName);

            // Assert
            Assert.True(ReferenceEquals(initialClient,result));
        }

        [Fact]
        public void GetSearchClientForIndex_WhenClientNotInitialized_ReturnsNewSearchClient()
        {
            // Arrange
            var indexName = "test-index";

            // Act
            var result = _factory.GetSearchClientForIndex(indexName);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SearchClient>(result);
        }

        [Fact]
        public void GetSearchClientForIndex_WhenClientIsInitialized_ReturnsSameSearchClient()
        {
            // Arrange
            var indexName = "test-index";

            // Act
            var initialClient = _factory.GetSearchClientForIndex(indexName);
            var result = _factory.GetSearchClientForIndex(indexName);

            // Assert
            Assert.True(ReferenceEquals(initialClient, result));
        }
    }
}
