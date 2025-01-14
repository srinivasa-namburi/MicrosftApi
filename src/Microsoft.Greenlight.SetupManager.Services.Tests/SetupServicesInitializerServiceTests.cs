using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Moq;

namespace Microsoft.Greenlight.SetupManager.Services.Tests
{
    public class SetupServicesInitializerServiceTest
    {
        private readonly Mock<ILogger<SetupServicesInitializerService>> _loggerMock;
        private readonly Mock<IDocumentProcessInfoService> _documentProcessInfoServiceMock;
        private readonly Mock<IDocumentLibraryInfoService> _documentLibraryInfoServiceMock;
        private readonly Mock<IAdditionalDocumentLibraryKernelMemoryRepository> _additionalDocumentLibraryKernelMemoryRepositoryMock;
        private readonly Mock<SearchClientFactory> _searchClientFactoryMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<IOptions<ServiceConfigurationOptions>> _serviceConfigurationOptionsMock;
        private readonly Mock<AzureCredentialHelper> _azureCredentialHelperMock;
        private readonly Mock<SearchIndexClient> _baseSearchIndexClientMock;
        private readonly SetupServicesInitializerService _unitUnderTest;
        private readonly Mock<IKernelMemoryRepository> _kernelMemoryRepositoryMock;


        public SetupServicesInitializerServiceTest()
        {
            _loggerMock = new Mock<ILogger<SetupServicesInitializerService>>();
            _documentProcessInfoServiceMock = new Mock<IDocumentProcessInfoService>();
            _documentLibraryInfoServiceMock = new Mock<IDocumentLibraryInfoService>();
            _additionalDocumentLibraryKernelMemoryRepositoryMock = new Mock<IAdditionalDocumentLibraryKernelMemoryRepository>();
            _kernelMemoryRepositoryMock = new Mock<IKernelMemoryRepository>();
            _configurationMock = new Mock<IConfiguration>();
            _serviceConfigurationOptionsMock = new Mock<IOptions<ServiceConfigurationOptions>>();
            _azureCredentialHelperMock = new Mock<AzureCredentialHelper>(_configurationMock.Object);
            _baseSearchIndexClientMock = new Mock<SearchIndexClient>();
            _searchClientFactoryMock = new Mock<SearchClientFactory>(_configurationMock.Object,
                _baseSearchIndexClientMock.Object,
                _serviceConfigurationOptionsMock.Object,
                _azureCredentialHelperMock.Object);

            IServiceCollection serviceCollection = new ServiceCollection()
                .AddScoped(provider => _documentProcessInfoServiceMock.Object)
                .AddScoped(provider => _documentLibraryInfoServiceMock.Object)
                .AddScoped(provider => _additionalDocumentLibraryKernelMemoryRepositoryMock.Object)
                .AddScoped(provider => _kernelMemoryRepositoryMock.Object);

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            _unitUnderTest = new SetupServicesInitializerService(
            serviceProvider, _loggerMock.Object, _searchClientFactoryMock.Object);
        }

        [Fact]
        public async Task ExecuteAsync_IndexAlreadyExists_DoesNotCallStoreContentAsync()
        {
            //Arrange
            var documentProcess = new DocumentProcessInfo
            {
                ShortName = "TestProcess",
                LogicType = DocumentProcessLogicType.KernelMemory,
                Repositories = new List<string> { "TestRepository" }
            };

            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo> { documentProcess });
            _documentProcessInfoServiceMock.Setup(x => x.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentProcess);

            var searchIndexClientMock = new Mock<SearchIndexClient>();
            _searchClientFactoryMock.Setup(x => x.GetSearchIndexClientForIndex(It.IsAny<string>()))
                .Returns(searchIndexClientMock.Object);

            var searchIndex = new SearchIndex("TestIndex");
            var responseMock = new Mock<Response<SearchIndex>>();
            responseMock.SetupGet(x => x.Value).Returns(searchIndex);

            searchIndexClientMock.Setup(x => x.GetIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMock.Object);

            _documentLibraryInfoServiceMock.Setup(x => x.GetAllDocumentLibrariesAsync())
                .ReturnsAsync(new List<DocumentLibraryInfo>());

            // Act
            await _unitUnderTest.StartAsync(CancellationToken.None);

            // Assert
            _additionalDocumentLibraryKernelMemoryRepositoryMock.Verify(
                x => x.StoreContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_NoKernelMemoryDocumentProcesses_DoesNotCallGetAllDocumentLibrariesAsync()
        {
            // Arrange
            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo>());

            // Act
            await _unitUnderTest.StartAsync(CancellationToken.None);

            // Assert
            _documentLibraryInfoServiceMock.Verify(x => x.GetAllDocumentLibrariesAsync(), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_IndexDoesNotExist_CreatesIndex()
        {
            //Arrange
            var documentProcess = new DocumentProcessInfo
            {
                ShortName = "TestProcess",
                LogicType = DocumentProcessLogicType.KernelMemory,
                Repositories = new List<string> { "TestRepository" }
            };

            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo> { documentProcess });
            _documentProcessInfoServiceMock.Setup(x => x.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentProcess);

            var searchIndexClientMock = new Mock<SearchIndexClient>();
            _searchClientFactoryMock.Setup(x => x.GetSearchIndexClientForIndex(It.IsAny<string>()))
                .Returns(searchIndexClientMock.Object);

            searchIndexClientMock.Setup(x => x.GetIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(404, "Index not found"));

            _documentLibraryInfoServiceMock.Setup(x => x.GetAllDocumentLibrariesAsync())
                .ReturnsAsync(new List<DocumentLibraryInfo>());

            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(x => x.GetService(typeof(IKernelMemoryRepository)))
                .Returns(_kernelMemoryRepositoryMock.Object);

            // Act
            await _unitUnderTest.StartAsync(CancellationToken.None);

            // Assert
            _kernelMemoryRepositoryMock.Verify(
                x => x.StoreContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_MultipleKernelMemoryRepositories_CreatesIndexes()
        {
            //Arrange
            var documentProcess = new DocumentProcessInfo
            {
                ShortName = "TestProcess",
                LogicType = DocumentProcessLogicType.KernelMemory,
                Repositories = new List<string> { "TestRepository1", "TestRepository2" }
            };

            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo> { documentProcess });
            _documentProcessInfoServiceMock.Setup(x => x.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentProcess);

            var searchIndexClientMock1 = new Mock<SearchIndexClient>();
            var searchIndexClientMock2 = new Mock<SearchIndexClient>();
            _searchClientFactoryMock.Setup(x => x.GetSearchIndexClientForIndex("TestRepository1"))
                .Returns(searchIndexClientMock1.Object);
            _searchClientFactoryMock.Setup(x => x.GetSearchIndexClientForIndex("TestRepository2"))
                .Returns(searchIndexClientMock2.Object);

            searchIndexClientMock1.Setup(x => x.GetIndexAsync("TestRepository1", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(404, "Index not found"));
            searchIndexClientMock2.Setup(x => x.GetIndexAsync("TestRepository2", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(404, "Index not found"));

            _documentLibraryInfoServiceMock.Setup(x => x.GetAllDocumentLibrariesAsync())
                .ReturnsAsync(new List<DocumentLibraryInfo>());

            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(x => x.GetService(typeof(IKernelMemoryRepository)))
                .Returns(_kernelMemoryRepositoryMock.Object);

            // Act
            await _unitUnderTest.StartAsync(CancellationToken.None);

            // Assert
            _kernelMemoryRepositoryMock.Verify(
                x => x.StoreContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()),
                Times.Exactly(2));
        }
    }
}