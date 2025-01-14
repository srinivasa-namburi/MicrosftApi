using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Testing;
using Moq;
using Xunit;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers.Tests
{
    public class DocumentIngestionSagaStartConsumerTests
    {
        private readonly Mock<ILogger<DocumentIngestionSagaStartConsumer>> _loggerMock;
        private readonly Mock<IDocumentProcessInfoService> _documentProcessInfoServiceMock;
        private readonly DocumentIngestionSagaStartConsumer _consumer;
        private readonly DocumentIngestionRequest _documentIngestionRequest;

        public DocumentIngestionSagaStartConsumerTests()
        {
            _loggerMock = new Mock<ILogger<DocumentIngestionSagaStartConsumer>>();
            _documentProcessInfoServiceMock = new Mock<IDocumentProcessInfoService>();
            _consumer = new DocumentIngestionSagaStartConsumer(
                _loggerMock.Object,
                _documentProcessInfoServiceMock.Object);
            _documentIngestionRequest = new DocumentIngestionRequest
            {
                Id = Guid.NewGuid(),
                DocumentLibraryShortName = "KernelMemory",
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                FileName = "test.pdf",
                OriginalDocumentUrl = "http://example.com/test.pdf",
                UploadedByUserOid = "user-oid",
                Plugin = "plugin"
            };
        }

        [Fact]
        public async Task Consume_ShouldPublishError_WhenDocumentProcessNotFound()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<DocumentIngestionRequest>>();
            contextMock.Setup(x => x.Message).Returns(_documentIngestionRequest);

            _documentProcessInfoServiceMock
                .Setup(x => 
                    x.GetDocumentProcessInfoByShortNameAsync(_documentIngestionRequest.DocumentLibraryShortName))
                .ReturnsAsync((DocumentProcessInfo?)null);

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Error);
        }

        [Fact]
        public async Task Consume_ShouldPublishKernelMemoryDocumentIngestionRequest_WhenLogicTypeIsKernelMemory()
        {
            // Arrange
            var documentProcessInfo = new DocumentProcessInfo
            {
                LogicType = DocumentProcessLogicType.KernelMemory
            };

            var contextMock = new Mock<ConsumeContext<DocumentIngestionRequest>>();

            contextMock.Setup(x => x.Message).Returns(_documentIngestionRequest);

            _documentProcessInfoServiceMock
                .Setup(x => 
                    x.GetDocumentProcessInfoByShortNameAsync(_documentIngestionRequest.DocumentLibraryShortName))
                .ReturnsAsync(documentProcessInfo);

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            contextMock.Verify(x => x.Publish(It.Is<KernelMemoryDocumentIngestionRequest>(doc =>
                    doc.CorrelationId == _documentIngestionRequest.Id &&
                    doc.DocumentLibraryShortName == _documentIngestionRequest.DocumentLibraryShortName &&
                    doc.DocumentLibraryType == _documentIngestionRequest.DocumentLibraryType &&
                    doc.FileName == _documentIngestionRequest.FileName &&
                    doc.OriginalDocumentUrl == _documentIngestionRequest.OriginalDocumentUrl &&
                    doc.UploadedByUserOid == _documentIngestionRequest.UploadedByUserOid), 
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldPublishClassicDocumentIngestionRequest_WhenLogicTypeIsNotKernelMemory()
        {
            // Arrange
            var documentProcessInfo = new DocumentProcessInfo
            {
                LogicType = DocumentProcessLogicType.Classic
            };

            var contextMock = new Mock<ConsumeContext<DocumentIngestionRequest>>();
            contextMock.Setup(x => x.Message).Returns(_documentIngestionRequest);

            _documentProcessInfoServiceMock
                .Setup(x => 
                    x.GetDocumentProcessInfoByShortNameAsync(_documentIngestionRequest.DocumentLibraryShortName))
                .ReturnsAsync(documentProcessInfo);

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            contextMock.Verify(x => x.Publish(
                It.Is<ClassicDocumentIngestionRequest>(doc =>
                    doc.CorrelationId == _documentIngestionRequest.Id &&
                    doc.FileName == _documentIngestionRequest.FileName &&
                    doc.OriginalDocumentUrl == _documentIngestionRequest.OriginalDocumentUrl &&
                    doc.UploadedByUserOid == _documentIngestionRequest.UploadedByUserOid &&
                    doc.Plugin == _documentIngestionRequest.Plugin), 
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldDefaultToKernelMemory_WhenDocumentLibraryTypeIsNotPrimaryDocumentProcessLibrary()
        {
            // Arrange
            var contextMock = new Mock<ConsumeContext<DocumentIngestionRequest>>();
            _documentIngestionRequest.DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary;
            contextMock.Setup(x => x.Message).Returns(_documentIngestionRequest);

            // Act
            await _consumer.Consume(contextMock.Object);

            // Assert
            contextMock.Verify(x => x.Publish(
                It.Is<KernelMemoryDocumentIngestionRequest>(doc =>
                    doc.CorrelationId == _documentIngestionRequest.Id &&
                    doc.DocumentLibraryShortName == _documentIngestionRequest.DocumentLibraryShortName &&
                    doc.DocumentLibraryType == _documentIngestionRequest.DocumentLibraryType &&
                    doc.FileName == _documentIngestionRequest.FileName &&
                    doc.OriginalDocumentUrl == _documentIngestionRequest.OriginalDocumentUrl &&
                    doc.UploadedByUserOid == _documentIngestionRequest.UploadedByUserOid), 
                It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
