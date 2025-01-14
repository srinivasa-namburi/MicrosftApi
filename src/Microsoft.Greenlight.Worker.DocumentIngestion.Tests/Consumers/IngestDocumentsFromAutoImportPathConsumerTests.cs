using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Testing;
using Moq;
using Xunit;
using Response = Azure.Response;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers.Tests
{
    public class IngestDocumentsFromAutoImportPathConsumerTests
    {
        private readonly Mock<BlobServiceClient> _blobServiceClientMock;
        private readonly Mock<ILogger<IngestDocumentsFromAutoImportPathConsumer>> _loggerMock;
        private readonly Mock<IDocumentProcessInfoService> _documentProcessInfoServiceMock;
        private readonly Mock<IDocumentLibraryInfoService> _documentLibraryInfoServiceMock;
        private readonly IngestDocumentsFromAutoImportPathConsumer _consumer;

        public IngestDocumentsFromAutoImportPathConsumerTests()
        {
            _blobServiceClientMock = new Mock<BlobServiceClient>();
            _loggerMock = new Mock<ILogger<IngestDocumentsFromAutoImportPathConsumer>>();
            _documentProcessInfoServiceMock = new Mock<IDocumentProcessInfoService>();
            _documentLibraryInfoServiceMock = new Mock<IDocumentLibraryInfoService>();

            _consumer = new IngestDocumentsFromAutoImportPathConsumer(
                _blobServiceClientMock.Object,
                _loggerMock.Object,
                _documentProcessInfoServiceMock.Object,
                _documentLibraryInfoServiceMock.Object);
        }

        [Fact]
        public async Task Consume_ShouldLogError_WhenDocumentLibraryShortNameIsNul()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<IngestDocumentsFromAutoImportPath>>();
            contextMock.Setup(c => c.Message).Returns(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
            {
                BlobContainerName = "container",
                FolderPath = "folder",
                DocumentLibraryShortName = null,
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary
            });

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        [Fact]
        public async Task Consume_ShouldLogError_WhenDocumentLibraryIsUnknown()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<IngestDocumentsFromAutoImportPath>>();
            contextMock.Setup(c => c.Message).Returns(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
            {
                BlobContainerName = "container",
                FolderPath = "folder",
                DocumentLibraryShortName = "UnknownLibrary",
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary
            });

            _documentLibraryInfoServiceMock
                .Setup(s => s.GetDocumentLibraryByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync((DocumentLibraryInfo?)null);

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        [Fact]
        public async Task Consume_ShouldLogError_WhenDocumentProcessIsUnknown()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<IngestDocumentsFromAutoImportPath>>();
            contextMock.Setup(c => c.Message).Returns(new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
            {
                BlobContainerName = "container",
                FolderPath = "folder",
                DocumentLibraryShortName = "UnknownProcess",
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary
            });

            _documentProcessInfoServiceMock
                .Setup(s => s.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .ReturnsAsync((DocumentProcessInfo?)null);

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        [Fact]
        public async Task Consume_ShouldCopyBlobsAndPublishMessage()
        {
            // Arrange
            var documentLibraryInfo = new DocumentLibraryInfo
            {
                ShortName = "Library",
                BlobStorageContainerName = "container"
            };

            var documentProcessInfo = new DocumentProcessInfo
            {
                ShortName = "Process",
                BlobStorageContainerName = "container"
            };

            var ingestDocsRequest = new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
            {
                DocumentLibraryShortName = "Library",
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
                BlobContainerName = "sourceContainer",
                FolderPath = "folder"
            };

            var contextMock = new Mock<ConsumeContext<IngestDocumentsFromAutoImportPath>>();
            var blob = BlobsModelFactory.BlobItem("blob");
            contextMock.Setup(c => c.Message).Returns(ingestDocsRequest);

            _documentLibraryInfoServiceMock
                .Setup(s => s.GetDocumentLibraryByShortNameAsync(ingestDocsRequest.DocumentLibraryShortName))
                .ReturnsAsync(documentLibraryInfo);

            var sourceContainerClientMock = new Mock<BlobContainerClient>();
            var sourceBlobClientMock = new Mock<BlobClient>();
            var targetContainerClientMock = new Mock<BlobContainerClient>();
            var targetBlobClientMock = new Mock<BlobClient>();
            var sourceUri = new Uri("http://sourceContainer/blob");
            var targetUri = new Uri("http://targetContainer/blob");

            _blobServiceClientMock
                .Setup(s => s.GetBlobContainerClient(ingestDocsRequest.BlobContainerName))
                .Returns(sourceContainerClientMock.Object);

            _blobServiceClientMock
                .Setup(s => s.GetBlobContainerClient(documentLibraryInfo.BlobStorageContainerName))
                .Returns(targetContainerClientMock.Object);

            sourceContainerClientMock
                .Setup(c => c.GetBlobClient(blob.Name))
                .Returns(sourceBlobClientMock.Object);

            sourceContainerClientMock
                .Setup(c =>
                    c.GetBlobsAsync(
                        It.IsAny<BlobTraits>(),
                        It.IsAny<BlobStates>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>())
                    )
                .Returns(AsyncPageable<BlobItem>
                    .FromPages([Page<BlobItem>.FromValues([blob], null, Mock.Of<Response>())]));

            targetContainerClientMock
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(targetBlobClientMock.Object);

            targetBlobClientMock
                .Setup(c => c.Uri)
                .Returns(targetUri);

            targetBlobClientMock
                .Setup(c => c.StartCopyFromUriAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<RehydratePriority?>(),
                    It.IsAny<CancellationToken>())
                    )
                .ReturnsAsync(Mock.Of<CopyFromUriOperation>());

            sourceBlobClientMock
                .Setup(c => 
                    c.DeleteIfExistsAsync(
                        It.IsAny<DeleteSnapshotsOption>(),
                        It.IsAny<BlobRequestConditions>(),
                        It.IsAny<CancellationToken>())
                    )
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            sourceBlobClientMock
                .Setup(c => c.Uri)
                .Returns(sourceUri);

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            targetContainerClientMock.Verify(c => c.GetBlobClient(It.Is<string>(s => s.Contains(blob.Name))), Times.Once);
            sourceContainerClientMock.Verify(c => c.GetBlobClient(blob.Name), Times.Once);
            targetBlobClientMock.Verify(c => c.StartCopyFromUriAsync(sourceUri,
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<RehydratePriority?>(),
                    It.IsAny<CancellationToken>()), 
                Times.Once);
            sourceBlobClientMock.Verify(c => 
                c.DeleteIfExistsAsync(
                    It.IsAny<DeleteSnapshotsOption>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()
                    ),
                Times.Once);
            contextMock.Verify(c => 
                c.Publish(
                    It.Is<DocumentIngestionRequest>(doc =>
                        doc.OriginalDocumentUrl == targetUri.ToString() &&
                        doc.DocumentLibraryType == ingestDocsRequest.DocumentLibraryType &&
                        doc.DocumentLibraryShortName == ingestDocsRequest.DocumentLibraryShortName &&
                        doc.FileName == targetUri.Segments.Last()
                    ), 
                    It.IsAny<CancellationToken>()
                   ), 
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldHandle409_WhenBlobAlreadyExists()
        {
            // Arrange
            var documentLibraryInfo = new DocumentLibraryInfo
            {
                ShortName = "Library",
                BlobStorageContainerName = "container"
            };

            var documentProcessInfo = new DocumentProcessInfo
            {
                ShortName = "Process",
                BlobStorageContainerName = "container"
            };

            var ingestDocsRequest = new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
            {
                DocumentLibraryShortName = "Library",
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
                BlobContainerName = "sourceContainer",
                FolderPath = "folder"
            };

            var contextMock = new Mock<ConsumeContext<IngestDocumentsFromAutoImportPath>>();
            var blob = BlobsModelFactory.BlobItem("blob");
            contextMock.Setup(c => c.Message).Returns(ingestDocsRequest);

            _documentLibraryInfoServiceMock
                .Setup(s => s.GetDocumentLibraryByShortNameAsync(ingestDocsRequest.DocumentLibraryShortName))
                .ReturnsAsync(documentLibraryInfo);

            var sourceContainerClientMock = new Mock<BlobContainerClient>();
            var sourceBlobClientMock = new Mock<BlobClient>();
            var targetContainerClientMock = new Mock<BlobContainerClient>();
            var targetBlobClientMock = new Mock<BlobClient>();
            var sourceUri = new Uri("http://sourceContainer/blob");
            var targetUri = new Uri("http://targetContainer/blob");

            _blobServiceClientMock
                .Setup(s => s.GetBlobContainerClient(ingestDocsRequest.BlobContainerName))
                .Returns(sourceContainerClientMock.Object);

            _blobServiceClientMock
                .Setup(s => s.GetBlobContainerClient(documentLibraryInfo.BlobStorageContainerName))
                .Returns(targetContainerClientMock.Object);

            sourceContainerClientMock
                .Setup(c => c.GetBlobClient(blob.Name))
                .Returns(sourceBlobClientMock.Object);

            sourceContainerClientMock
                .Setup(c =>
                    c.GetBlobsAsync(
                        It.IsAny<BlobTraits>(),
                        It.IsAny<BlobStates>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>())
                    )
                .Returns(AsyncPageable<BlobItem>
                    .FromPages([Page<BlobItem>.FromValues([blob], null, Mock.Of<Response>())]));

            targetContainerClientMock
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(targetBlobClientMock.Object);

            targetBlobClientMock
                .Setup(c => c.Uri)
                .Returns(targetUri);

            targetBlobClientMock
                .Setup(c => c.StartCopyFromUriAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<RehydratePriority?>(),
                    It.IsAny<CancellationToken>())
                    )
                .ThrowsAsync(new RequestFailedException(409, "Conflict", "Conflict", null));

            sourceBlobClientMock
                .Setup(c => 
                    c.DeleteIfExistsAsync(
                        It.IsAny<DeleteSnapshotsOption>(),
                        It.IsAny<BlobRequestConditions>(),
                        It.IsAny<CancellationToken>())
                    )
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            sourceBlobClientMock
                .Setup(c => c.Uri)
                .Returns(sourceUri);

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            targetContainerClientMock.Verify(c => c.GetBlobClient(It.Is<string>(s => s.Contains(blob.Name))), Times.Once);
            sourceContainerClientMock.Verify(c => c.GetBlobClient(blob.Name), Times.Once);
            targetBlobClientMock.Verify(c => c.StartCopyFromUriAsync(sourceUri,
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<RehydratePriority?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            sourceBlobClientMock.Verify(c =>
                c.DeleteIfExistsAsync(
                    It.IsAny<DeleteSnapshotsOption>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()
                    ),
                Times.Once);

            contextMock.Verify(c =>
                c.Publish(
                    It.Is<DocumentIngestionRequest>(doc =>
                        doc.OriginalDocumentUrl == targetUri.ToString() &&
                        doc.DocumentLibraryType == ingestDocsRequest.DocumentLibraryType &&
                        doc.DocumentLibraryShortName == ingestDocsRequest.DocumentLibraryShortName &&
                        doc.FileName == targetUri.Segments.Last()
                    ),
                    It.IsAny<CancellationToken>()
                   ),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldLogError_WhenCopyFails()
        {
            // Arrange
            var documentLibraryInfo = new DocumentLibraryInfo
            {
                ShortName = "Library",
                BlobStorageContainerName = "container"
            };

            var documentProcessInfo = new DocumentProcessInfo
            {
                ShortName = "Process",
                BlobStorageContainerName = "container"
            };

            var ingestDocsRequest = new IngestDocumentsFromAutoImportPath(Guid.NewGuid())
            {
                DocumentLibraryShortName = "Library",
                DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
                BlobContainerName = "sourceContainer",
                FolderPath = "folder"
            };

            var contextMock = new Mock<ConsumeContext<IngestDocumentsFromAutoImportPath>>();
            var blob = BlobsModelFactory.BlobItem("blob");
            contextMock.Setup(c => c.Message).Returns(ingestDocsRequest);

            _documentLibraryInfoServiceMock
                .Setup(s => s.GetDocumentLibraryByShortNameAsync(ingestDocsRequest.DocumentLibraryShortName))
                .ReturnsAsync(documentLibraryInfo);

            var sourceContainerClientMock = new Mock<BlobContainerClient>();
            var sourceBlobClientMock = new Mock<BlobClient>();
            var targetContainerClientMock = new Mock<BlobContainerClient>();
            var targetBlobClientMock = new Mock<BlobClient>();
            var sourceUri = new Uri("http://sourceContainer/blob");
            var targetUri = new Uri("http://targetContainer/blob");

            _blobServiceClientMock
                .Setup(s => s.GetBlobContainerClient(ingestDocsRequest.BlobContainerName))
                .Returns(sourceContainerClientMock.Object);

            _blobServiceClientMock
                .Setup(s => s.GetBlobContainerClient(documentLibraryInfo.BlobStorageContainerName))
                .Returns(targetContainerClientMock.Object);

            sourceContainerClientMock
                .Setup(c => c.GetBlobClient(blob.Name))
                .Returns(sourceBlobClientMock.Object);

            sourceContainerClientMock
                .Setup(c => 
                    c.GetBlobsAsync(
                        It.IsAny<BlobTraits>(), 
                        It.IsAny<BlobStates>(), 
                        It.IsAny<string>(), 
                        It.IsAny<CancellationToken>())
                    )
                .Returns(AsyncPageable<BlobItem>
                    .FromPages([Page<BlobItem>.FromValues([blob], null, Mock.Of<Response>())]));

            targetContainerClientMock
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(targetBlobClientMock.Object);

            targetBlobClientMock
                .Setup(c => c.Uri)
                .Returns(new Uri("http://container"));

            targetBlobClientMock
                .Setup(c => c.StartCopyFromUriAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<RehydratePriority?>(),
                    It.IsAny<CancellationToken>())
                    )
                .ThrowsAsync(new RequestFailedException(500, "Server Error", "Server Error", null));

            sourceBlobClientMock
                .Setup(c =>
                    c.DeleteIfExistsAsync(
                        It.IsAny<DeleteSnapshotsOption>(),
                        It.IsAny<BlobRequestConditions>(),
                        It.IsAny<CancellationToken>())
                    )
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            sourceBlobClientMock
                .Setup(c => c.Uri)
                .Returns(sourceUri);

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            targetContainerClientMock.Verify(c => c.GetBlobClient(It.Is<string>(s => s.Contains(blob.Name))), Times.Once);
            sourceContainerClientMock.Verify(c => c.GetBlobClient(blob.Name), Times.Once);
            targetBlobClientMock.Verify(c => c.StartCopyFromUriAsync(sourceUri,
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<RehydratePriority?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _loggerMock.VerifyLog(LogLevel.Error);
        }
    }
}
