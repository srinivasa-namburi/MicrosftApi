using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.API.Main.Consumers;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;
using Response = Azure.Response;

namespace Microsoft.Greenlight.API.Main.Tests.Consumers
{
    public sealed class CleanupExportedDocumentConsumerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public CleanupExportedDocumentConsumerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class CleanupExportedDocumentConsumerTests : IClassFixture<CleanupExportedDocumentConsumerFixture>
    {
        private readonly Mock<BlobClient> _mockBlobClient = new();
        private readonly Mock<BlobContainerClient> _mockBlobContainerClient = new();
        private readonly Mock<BlobServiceClient> _mockBlobServiceClient = new();
        private readonly Mock<ConsumeContext<CleanupExportedDocument>> _mockConsumeContext = new();
        private readonly Mock<ILogger<CleanupExportedDocumentConsumer>> _loggerMock = new(); 
        private readonly DocGenerationDbContext _docGenerationDbContext;

        public CleanupExportedDocumentConsumerTests(CleanupExportedDocumentConsumerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _mockBlobContainerClient.Setup(b => b.GetBlobClient(It.IsAny<string>()))
                .Returns(_mockBlobClient.Object);
            _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(_mockBlobContainerClient.Object);
        }

        [Fact]
        public async Task Consume_WhenBlobContainerIsNullOrEmpty_ShouldNotCallDeleteIfExistsAsync()
        {
            // Arrange
            var consumer = new CleanupExportedDocumentConsumer
            (
                _mockBlobServiceClient.Object, 
                _docGenerationDbContext, 
                _loggerMock.Object
            );
            var message = new CleanupExportedDocument(Guid.NewGuid())
            {
                BlobContainer = string.Empty,
                FileName = "test-file"
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(message);
            var _consumer = new CleanupExportedDocumentConsumer(_mockBlobServiceClient.Object, _docGenerationDbContext, _loggerMock.Object);

            // Act
            await _consumer.Consume(_mockConsumeContext.Object);

            // Assert
            _mockBlobClient.Verify(b => b.DeleteIfExistsAsync(
                 It.IsAny<DeleteSnapshotsOption>(),
                 It.IsAny<BlobRequestConditions>(),
                 It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Consume_WhenFileNameIsNullOrEmpty_ShouldNotCallDeleteIfExistsAsync()
        {
            // Arrange
            var consumer = new CleanupExportedDocumentConsumer
            (
                _mockBlobServiceClient.Object,
                _docGenerationDbContext,
                _loggerMock.Object
            );
            var message = new CleanupExportedDocument(Guid.NewGuid())
            {
                BlobContainer = "test-container",
                FileName = string.Empty
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(message);
            var _consumer = new CleanupExportedDocumentConsumer(_mockBlobServiceClient.Object, _docGenerationDbContext, _loggerMock.Object);

            // Act
            await _consumer.Consume(_mockConsumeContext.Object);

            // Assert
            _mockBlobClient.Verify(b => b.DeleteIfExistsAsync(
                 It.IsAny<DeleteSnapshotsOption>(),
                 It.IsAny<BlobRequestConditions>(),
                 It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Consume_WhenBlobIsMarkedForDeletionAndDeletedFilesExist_ShouldCallExecuteDeleteAsync()
        {
            // Arrange
            _mockBlobClient
                .Setup(b => b.DeleteIfExistsAsync(
                    It.IsAny<DeleteSnapshotsOption>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

            var exportedDocumentLink = new ExportedDocumentLink
            {
                Id = Guid.NewGuid(),
                BlobContainer = "marked-container",
                FileName = "test-file",
                MimeType = "application/pdf",
                AbsoluteUrl = "https://example.com/test-file"
            };
            _docGenerationDbContext.ExportedDocumentLinks.Add(exportedDocumentLink);
            _docGenerationDbContext.SaveChanges();

            var message = new CleanupExportedDocument(Guid.NewGuid())
            {
                BlobContainer = "marked-container",
                FileName = "test-file",
                ExportedDocumentLinkId = exportedDocumentLink.Id
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(message);

            var consumer = new CleanupExportedDocumentConsumer
            (
                _mockBlobServiceClient.Object,
                _docGenerationDbContext,
                _loggerMock.Object
            );

            // Act
            await consumer.Consume(_mockConsumeContext.Object);

            // Assert
            _mockBlobClient.Verify(b => b.DeleteIfExistsAsync(
                It.Is<DeleteSnapshotsOption>(d => d == DeleteSnapshotsOption.None),
                It.Is<BlobRequestConditions>(c => c == null),
                It.IsAny<CancellationToken>()), Times.Once);

            var deletedDocumentLink = await _docGenerationDbContext.ExportedDocumentLinks
                .FirstOrDefaultAsync(e => e.Id == exportedDocumentLink.Id);
            Assert.Null(deletedDocumentLink);
        }

    }

}
