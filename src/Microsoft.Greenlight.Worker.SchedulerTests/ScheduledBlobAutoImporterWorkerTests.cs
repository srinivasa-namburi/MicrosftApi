using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Services;
using Moq;
using Xunit;
using Microsoft.Greenlight.Shared.Testing;
using Azure;
using Response = Azure.Response;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

namespace Microsoft.Greenlight.Worker.Scheduler.Tests
{
    public class ScheduledBlobAutoImportWorkerTests
    {
        private readonly Mock<ILogger<ScheduledBlobAutoImportWorker>> _loggerMock;
        private readonly Mock<BlobServiceClient> _blobServiceClientMock;
        private readonly Mock<IDocumentProcessInfoService> _documentProcessInfoServiceMock;
        private readonly Mock<IDocumentLibraryInfoService> _documentLibraryInfoServiceMock;
        private readonly Mock<IOptions<ServiceConfigurationOptions>> _optionsMock;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<IOptionsMonitor<ServiceConfigurationOptions>> _optionsMonitorMock;

        public ScheduledBlobAutoImportWorkerTests()
        {
            _loggerMock = new Mock<ILogger<ScheduledBlobAutoImportWorker>>();
            _blobServiceClientMock = new Mock<BlobServiceClient>();
            _documentProcessInfoServiceMock = new Mock<IDocumentProcessInfoService>();
            _documentLibraryInfoServiceMock = new Mock<IDocumentLibraryInfoService>();
            _publishEndpointMock = new Mock<IPublishEndpoint>();

            _optionsMock = new Mock<IOptions<ServiceConfigurationOptions>>();
            _optionsMonitorMock = new Mock<IOptionsMonitor<ServiceConfigurationOptions>>();

            var options = new ServiceConfigurationOptions
            {
                GreenlightServices = new ServiceConfigurationOptions.GreenlightServicesOptions
                {
                    DocumentIngestion = new ServiceConfigurationOptions.GreenlightServicesOptions.DocumentIngestionOptions
                    {
                        ScheduledIngestion = true
                    }
                }
            };

            _optionsMock.Setup(x => x.Value).Returns(options);
            _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(options);

            var serviceCollection = new ServiceCollection()
                .AddScoped(provider => _documentProcessInfoServiceMock.Object)
                .AddScoped(provider => _documentLibraryInfoServiceMock.Object)
                .AddScoped(provider => _publishEndpointMock.Object);
            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public async Task ExecuteAsync_WhenNoDocumentProcessesOrLibrariesExist_ShouldNotPublishEvents()
        {
            // Arrange
            _documentProcessInfoServiceMock
                .Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo>());

            var worker = new ScheduledBlobAutoImportWorker(
                _loggerMock.Object,
                _optionsMonitorMock.Object,
                _blobServiceClientMock.Object,
                _serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            _documentProcessInfoServiceMock.Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo>());
            _documentLibraryInfoServiceMock.Setup(x => x.GetAllDocumentLibrariesAsync())
                .ReturnsAsync(new List<DocumentLibraryInfo>());

            var cancellationToken = new CancellationTokenSource().Token;
            var publishEndpoint = _serviceProvider.GetRequiredService<IPublishEndpoint>();

            // Act
            await worker.StartAsync(cancellationToken);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Warning);
            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<IngestDocumentsFromAutoImportPath>(), cancellationToken), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenContainerOrFolderMissing_ShouldNotPublishDocumentIngestionRequest()
        {
            // Arrange
            var documentProcessInfo = new DocumentProcessInfo
            {
                ShortName = "TestProcess",
            };
            var documentLibraryInfo = new DocumentLibraryInfo
            {
                ShortName = "TestLibrary"
            };

            _documentProcessInfoServiceMock
                .Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo> { documentProcessInfo });
            _documentLibraryInfoServiceMock
                .Setup(x => x.GetAllDocumentLibrariesAsync())
                .ReturnsAsync(new List<DocumentLibraryInfo> { documentLibraryInfo });

            var worker = new ScheduledBlobAutoImportWorker(
                _loggerMock.Object,
                _optionsMonitorMock.Object,
                _blobServiceClientMock.Object,
                _serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );
            var cancellationToken = new CancellationTokenSource().Token;

            // Act
            await worker.StartAsync(cancellationToken);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Warning);
            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<IngestDocumentsFromAutoImportPath>(), cancellationToken), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenContainerAndFolderSpecified_ShouldPublishDocumentIngestionRequests()
        {
            // Arrange
            var documentProcessInfo = new DocumentProcessInfo
            {
                BlobStorageAutoImportFolderName = "TestProcessFolder",
                BlobStorageContainerName = "TestProcessContainer",
                ShortName = "TestProcess",
            };
            var documentLibraryInfo = new DocumentLibraryInfo
            {
                BlobStorageAutoImportFolderName = "TestLibraryFolder",
                BlobStorageContainerName = "TestLibraryContainer",
                ShortName = "TestLibrary"
            };

            _documentProcessInfoServiceMock
                .Setup(x => x.GetCombinedDocumentProcessInfoListAsync())
                .ReturnsAsync(new List<DocumentProcessInfo> { documentProcessInfo });
            _documentLibraryInfoServiceMock
                .Setup(x => x.GetAllDocumentLibrariesAsync())
                .ReturnsAsync(new List<DocumentLibraryInfo> { documentLibraryInfo });

            var blobContainerClientMock = new Mock<BlobContainerClient>();
            _blobServiceClientMock.Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(blobContainerClientMock.Object);

            blobContainerClientMock
                .Setup(c => c.Exists(It.IsAny<CancellationToken>()))
                .Returns(Response.FromValue(true, Mock.Of<Response>()));

            var blob = BlobsModelFactory.BlobItem("blob");
            blobContainerClientMock.Setup(c =>
                    c.GetBlobs(
                        It.IsAny<BlobTraits>(),
                        It.IsAny<BlobStates>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>())
                    )
            .Returns(Pageable<BlobItem>
                    .FromPages([Page<BlobItem>.FromValues([blob], null, Mock.Of<Response>())]));

            var worker = new ScheduledBlobAutoImportWorker(
                _loggerMock.Object,
                _optionsMonitorMock.Object,
                _blobServiceClientMock.Object,
                _serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );
            var cancellationToken = new CancellationTokenSource().Token;

            // Act
            await worker.StartAsync(cancellationToken);

            // Assert
            // Verify AutoImport published for Document Library
            _publishEndpointMock.Verify(
                x => x.Publish(It.Is<IngestDocumentsFromAutoImportPath>(
                    r => r.DocumentLibraryShortName == documentLibraryInfo.ShortName &&
                    r.FolderPath == documentLibraryInfo.BlobStorageAutoImportFolderName &&
                    r.BlobContainerName == documentLibraryInfo.BlobStorageContainerName), It.IsAny<CancellationToken>())
                , Times.Once);

            // Verify AutoImport published for Document Process
            _publishEndpointMock.Verify(
                x => x.Publish(It.Is<IngestDocumentsFromAutoImportPath>(
                    r => r.DocumentLibraryShortName == documentProcessInfo.ShortName &&
                    r.FolderPath == documentProcessInfo.BlobStorageAutoImportFolderName &&
                    r.BlobContainerName == documentProcessInfo.BlobStorageContainerName), It.IsAny<CancellationToken>())
                , Times.Once);
        }
    }
}

