using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Moq;
using Xunit;
using Microsoft.Greenlight.Shared.Helpers;
using Azure.Storage.Blobs;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs.Models;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers.KernelMemoryDocumentIngestionSaga.Tests
{
    public class KernelMemoryCreateIngestedDocumentConsumerTests
    {
        private readonly Mock<ILogger<KernelMemoryCreateIngestedDocumentConsumer>> _loggerMock;
        private readonly AzureFileHelper _azureFileHelper;
        private readonly Mock<IDocumentProcessInfoService> _documentProcessInfoServiceMock;
        private readonly Mock<IDocumentLibraryInfoService> _documentLibraryInfoServiceMock;
        private readonly Mock<IKernelMemoryRepository> _kernelMemoryRepositoryMock;
        private readonly Mock<IAdditionalDocumentLibraryKernelMemoryRepository> _additionalDocumentLibraryRepositoryMock;
        private readonly Mock<BlobClient> _blobClientMock;

        public KernelMemoryCreateIngestedDocumentConsumerTests()
        {
            var options = new DbContextOptionsBuilder<DocGenerationDbContext>().Options;
            var dbContextMock = new Mock<DocGenerationDbContext>(options);
            _loggerMock = new Mock<ILogger<KernelMemoryCreateIngestedDocumentConsumer>>();
            var blobServiceClientMock = new Mock<BlobServiceClient>();
            _azureFileHelper = new AzureFileHelper(blobServiceClientMock.Object, dbContextMock.Object);

            _documentProcessInfoServiceMock = new Mock<IDocumentProcessInfoService>();
            _documentLibraryInfoServiceMock = new Mock<IDocumentLibraryInfoService>();
            _kernelMemoryRepositoryMock = new Mock<IKernelMemoryRepository>();
            _additionalDocumentLibraryRepositoryMock = new Mock<IAdditionalDocumentLibraryKernelMemoryRepository>();

            var blobContainerClientMock = new Mock<BlobContainerClient>();
            _blobClientMock = new Mock<BlobClient>();

            blobServiceClientMock
                .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(blobContainerClientMock.Object);

            blobContainerClientMock
                .Setup(x => x.GetBlobClient(It.IsAny<string>()))
                .Returns(_blobClientMock.Object);
        }

        [Fact]
        public async Task Consume_ShouldPublishFailedEvent_WhenDocumentLibraryShortNameIsNull()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<KernelMemoryCreateIngestedDocument>>();
            var message = new KernelMemoryCreateIngestedDocument(Guid.NewGuid())
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                DocumentLibraryShortName = null,
                OriginalDocumentUrl = "http://example.com/document",
                FileName = "document.pdf",
                UploadedByUserOid = "user-oid"
            };

            contextMock.Setup(x => x.Message).Returns(message);


            var serviceCollection = new ServiceCollection()
                .AddScoped(provider => _documentProcessInfoServiceMock.Object);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var consumer = new KernelMemoryCreateIngestedDocumentConsumer(
                _loggerMock.Object,
                _azureFileHelper,
                serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            contextMock.Verify(
                x => x.Publish(
                    It.Is<KernelMemoryDocumentIngestionFailed>(doc => doc.CorrelationId == message.CorrelationId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Consume_ShouldPublishFailedEvent_WhenPrimaryKernelLibraryRepositoryIsNull()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<KernelMemoryCreateIngestedDocument>>();
            var message = new KernelMemoryCreateIngestedDocument(Guid.NewGuid())
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                DocumentLibraryShortName = "PrimaryLibrary",
                OriginalDocumentUrl = "http://example.com/document",
                FileName = "document.pdf",
                UploadedByUserOid = "user-oid"
            };

            var documentProcessInfo = new DocumentProcessInfo
            {
                Repositories = ["test_repository"]
            };

            contextMock.Setup(x => x.Message).Returns(message);

            _documentProcessInfoServiceMock
                .Setup(x => x.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentProcessInfo);

            var serviceCollection = new ServiceCollection()
                .AddScoped(provider => _documentProcessInfoServiceMock.Object);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var consumer = new KernelMemoryCreateIngestedDocumentConsumer(
                _loggerMock.Object,
                _azureFileHelper,
                serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            _documentProcessInfoServiceMock.Verify(
                x => x.GetDocumentProcessInfoByShortNameAsync(It.Is<string>(s => s == message.DocumentLibraryShortName))
            );
            contextMock.Verify(
                x => x.Publish(
                    It.Is<KernelMemoryDocumentIngestionFailed>(doc => doc.CorrelationId == message.CorrelationId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Consume_ShouldPublishFailedEvent_WhenPrimaryDocumentLibraryFileStreamIsNull()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<KernelMemoryCreateIngestedDocument>>();
            var message = new KernelMemoryCreateIngestedDocument(Guid.NewGuid())
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                DocumentLibraryShortName = "PrimaryLibrary",
                OriginalDocumentUrl = "http://example.com/document",
                FileName = "document.pdf",
                UploadedByUserOid = "user-oid"
            };

            var documentProcessInfo = new DocumentProcessInfo
            {
                Repositories = ["test_repository"]
            };

            contextMock.Setup(x => x.Message).Returns(message);

            _documentProcessInfoServiceMock
                .Setup(x => x.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentProcessInfo);

            _blobClientMock
                .Setup(x => x.OpenReadAsync(
                    It.IsAny<long>(),
                    It.IsAny<int?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync((Stream?)null);

            var serviceCollection = new ServiceCollection()
                .AddScoped(provider => _kernelMemoryRepositoryMock.Object)
                .AddScoped(provider => _documentProcessInfoServiceMock.Object);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var consumer = new KernelMemoryCreateIngestedDocumentConsumer(
                _loggerMock.Object,
                _azureFileHelper,
                serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            _documentProcessInfoServiceMock.Verify(
                x => x.GetDocumentProcessInfoByShortNameAsync(It.Is<string>(s => s == message.DocumentLibraryShortName))
            );
            contextMock.Verify(
                x => x.Publish(
                    It.Is<KernelMemoryDocumentIngestionFailed>(doc => doc.CorrelationId == message.CorrelationId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Consume_ShouldPublishKernelMemoryDocumentCreated_WhenPrimaryDocumentLibraryTypeIsProcessed()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<KernelMemoryCreateIngestedDocument>>();
            var message = new KernelMemoryCreateIngestedDocument(Guid.NewGuid())
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                DocumentLibraryShortName = "PrimaryLibrary",
                OriginalDocumentUrl = "http://example.com/document",
                FileName = "document.pdf",
                UploadedByUserOid = "user-oid"
            };

            var documentProcessInfo = new DocumentProcessInfo
            {
                Repositories = ["test_repository"]
            };

            contextMock.Setup(x => x.Message).Returns(message);

            _documentProcessInfoServiceMock
                .Setup(x => x.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentProcessInfo);

            _blobClientMock
                .Setup(x => x.OpenReadAsync(
                    It.IsAny<long>(),
                    It.IsAny<int?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(new MemoryStream());

            var serviceCollection = new ServiceCollection()
                .AddScoped(provider => _kernelMemoryRepositoryMock.Object)
                .AddScoped(provider => _documentProcessInfoServiceMock.Object);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var consumer = new KernelMemoryCreateIngestedDocumentConsumer(
                _loggerMock.Object,
                _azureFileHelper,
                serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            _documentProcessInfoServiceMock.Verify(
                x => x.GetDocumentProcessInfoByShortNameAsync(It.Is<string>(s => s == message.DocumentLibraryShortName))
            );
            _kernelMemoryRepositoryMock.Verify(
                x => x.StoreContentAsync(
                    message.DocumentLibraryShortName,
                    It.Is<string>(s => s == documentProcessInfo.Repositories[0]),
                    It.IsAny<Stream>(),
                    message.FileName,
                    message.OriginalDocumentUrl,
                    message.UploadedByUserOid,
                    It.IsAny<Dictionary<string, string>?>()
                ),
                Times.Once
            );
            contextMock.Verify(
                x => x.Publish(
                    It.Is<KernelMemoryDocumentCreated>(doc => doc.CorrelationId == message.CorrelationId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Consume_ShouldPublishFailedEvent_WhenAdditionalDocumentLibraryInfoIsNull()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<KernelMemoryCreateIngestedDocument>>();
            var message = new KernelMemoryCreateIngestedDocument(Guid.NewGuid())
            {
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
                DocumentLibraryShortName = "AdditionalDocumentLibrary",
                OriginalDocumentUrl = "http://example.com/document",
                FileName = "document.pdf",
                UploadedByUserOid = "user-oid"
            };

            _documentLibraryInfoServiceMock
                .Setup(x => x.GetDocumentLibraryByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync((DocumentLibraryInfo?)null);

            contextMock.Setup(x => x.Message).Returns(message);

            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var consumer = new KernelMemoryCreateIngestedDocumentConsumer(
                _loggerMock.Object,
                _azureFileHelper,
                serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            contextMock.Verify(
                x => x.Publish(
                    It.Is<KernelMemoryDocumentIngestionFailed>(doc => doc.CorrelationId == message.CorrelationId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Consume_ShouldPublishFailedEvent_WhenAdditionalDocumentLibraryRepositoryIsNull()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<KernelMemoryCreateIngestedDocument>>();
            var message = new KernelMemoryCreateIngestedDocument(Guid.NewGuid())
            {
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
                DocumentLibraryShortName = "AdditionalDocumentLibrary",
                OriginalDocumentUrl = "http://example.com/document",
                FileName = "document.pdf",
                UploadedByUserOid = "user-oid"
            };

            var documentLibraryInfo = new DocumentLibraryInfo
            {
                IndexName = "test_index"
            };

            _documentLibraryInfoServiceMock
                .Setup(x => x.GetDocumentLibraryByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentLibraryInfo);

            contextMock.Setup(x => x.Message).Returns(message);

            var serviceCollection = new ServiceCollection();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var consumer = new KernelMemoryCreateIngestedDocumentConsumer(
                _loggerMock.Object,
                _azureFileHelper,
                serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            contextMock.Verify(
                x => x.Publish(
                    It.Is<KernelMemoryDocumentIngestionFailed>(doc => doc.CorrelationId == message.CorrelationId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Consume_ShouldPublishFailedEvent_WhenAdditionalDocumentLibraryFileStreamIsNull()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<KernelMemoryCreateIngestedDocument>>();
            var message = new KernelMemoryCreateIngestedDocument(Guid.NewGuid())
            {
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
                DocumentLibraryShortName = "AdditionalDocumentLibrary",
                OriginalDocumentUrl = "http://example.com/document",
                FileName = "document.pdf",
                UploadedByUserOid = "user-oid"
            };

            var documentLibraryInfo = new DocumentLibraryInfo
            {
                IndexName = "test_index"
            };

            _documentLibraryInfoServiceMock
                .Setup(x => x.GetDocumentLibraryByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentLibraryInfo);

            contextMock.Setup(x => x.Message).Returns(message);

            var serviceCollection = new ServiceCollection()
                .AddScoped(provider => _additionalDocumentLibraryRepositoryMock.Object);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            _blobClientMock
                .Setup(x => x.OpenReadAsync(
                    It.IsAny<long>(),
                    It.IsAny<int?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync((Stream?)null);

            var consumer = new KernelMemoryCreateIngestedDocumentConsumer(
                _loggerMock.Object,
                _azureFileHelper,
                serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            contextMock.Verify(
                x => x.Publish(
                    It.Is<KernelMemoryDocumentIngestionFailed>(doc => doc.CorrelationId == message.CorrelationId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Consume_ShouldPublishFailedEvent_WhenPrimaryDocumentProcessInfoIsNull()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<KernelMemoryCreateIngestedDocument>>();
            var message = new KernelMemoryCreateIngestedDocument(Guid.NewGuid())
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                DocumentLibraryShortName = "PrimaryLibrary",
                OriginalDocumentUrl = "http://example.com/document",
                FileName = "document.pdf",
                UploadedByUserOid = "user-oid"
            };

            contextMock.Setup(x => x.Message).Returns(message);

            _documentProcessInfoServiceMock
                .Setup(x => x.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync((DocumentProcessInfo?)null);

            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var consumer = new KernelMemoryCreateIngestedDocumentConsumer(
                _loggerMock.Object,
                _azureFileHelper,
                serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            contextMock.Verify(
                x => x.Publish(
                    It.Is<KernelMemoryDocumentIngestionFailed>(doc => doc.CorrelationId == message.CorrelationId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task Consume_ShouldPublishKernelMemoryDocumentCreated_WhenAdditionalDocumentLibraryTypeIsProcessed()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<KernelMemoryCreateIngestedDocument>>();
            var message = new KernelMemoryCreateIngestedDocument(Guid.NewGuid())
            {
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
                DocumentLibraryShortName = "AdditionalLibrary",
                OriginalDocumentUrl = "http://example.com/document",
                FileName = "document.pdf",
                UploadedByUserOid = "user-oid"
            };
            contextMock.Setup(x => x.Message).Returns(message);

            var documentLibraryInfo = new DocumentLibraryInfo
            {
                IndexName = "test_index"
            };

            _documentLibraryInfoServiceMock
                .Setup(x => x.GetDocumentLibraryByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync(documentLibraryInfo);

            _blobClientMock
                .Setup(x => x.OpenReadAsync(
                    It.IsAny<long>(),
                    It.IsAny<int?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(new MemoryStream());

            var serviceCollection = new ServiceCollection()
                .AddScoped(provider => _additionalDocumentLibraryRepositoryMock.Object);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var consumer = new KernelMemoryCreateIngestedDocumentConsumer(
                _loggerMock.Object,
                _azureFileHelper,
                serviceProvider,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object
            );

            // Act
            await consumer.Consume(contextMock.Object);

            // Assert
            _documentLibraryInfoServiceMock.Verify(x => x.GetDocumentLibraryByShortNameAsync(It.IsAny<string>()));
            _additionalDocumentLibraryRepositoryMock.Verify(
                x => x.StoreContentAsync(
                    message.DocumentLibraryShortName,
                    documentLibraryInfo.IndexName,
                    It.IsAny<Stream>(),
                    message.FileName,
                    message.OriginalDocumentUrl,
                    message.UploadedByUserOid,
                    It.IsAny<Dictionary<string, string>?>()
                ),
                Times.Once
            );
            contextMock.Verify(
                x => x.Publish(
                    It.Is<KernelMemoryDocumentCreated>(doc => doc.CorrelationId == message.CorrelationId),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }
    }
}
