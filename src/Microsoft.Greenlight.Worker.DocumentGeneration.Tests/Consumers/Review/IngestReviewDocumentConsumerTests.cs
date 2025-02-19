using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;
using Xunit;
using Azure.Storage.Blobs.Models;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.Review.Tests
{
    public sealed class IngestReviewDocumentConsumerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();

        public DocGenerationDbContext DocGenerationDbContext { get; }

        public IngestReviewDocumentConsumerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }

        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class IngestReviewDocumentConsumerTests : IClassFixture<IngestReviewDocumentConsumerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;

        // Default Fakes
        private readonly ILogger<IngestReviewDocumentConsumer> _fakeLogger =
            new Mock<ILogger<IngestReviewDocumentConsumer>>().Object;
        private readonly DbContextOptions<DocGenerationDbContext> _fakeDbContextOptions = new();
        private readonly IReviewKernelMemoryRepository _fakeRepository =
            new Mock<IReviewKernelMemoryRepository>().Object;

        // Default Mocks
        private readonly Mock<Stream> _mockStream = new();
        private readonly Mock<BlobClient> _mockBlobClient = new();
        private readonly Mock<BlobContainerClient> _mockBlobContainerClient = new();
        private readonly Mock<BlobServiceClient> _mockBlobServiceClient = new();
        private readonly Mock<ConsumeContext<IngestReviewDocument>> _mockConsumeContext = new();
        private readonly Mock<DocGenerationDbContext> _mockDocGenerationDbContext;
        private readonly Mock<AzureFileHelper> _mockAzureFileHelper;

        public IngestReviewDocumentConsumerTests(IngestReviewDocumentConsumerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _mockDocGenerationDbContext = new Mock<DocGenerationDbContext>(_fakeDbContextOptions);
            _mockAzureFileHelper =
                new Mock<AzureFileHelper>(_mockBlobServiceClient.Object, _mockDocGenerationDbContext.Object);
            _mockBlobContainerClient
                .Setup(client => client.GetBlobClient(It.IsAny<string>()))
                .Returns(_mockBlobClient.Object);
            _mockBlobServiceClient
                .Setup(client => client.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(_mockBlobContainerClient.Object);
        }

        [Fact]
        public async void Consume_WithoutCorrelatedReviewInstance_DoesNothing()
        {
            // Arrange
            // Ingest Review Document Message
            var messageId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var fakeReviewQuestionMessage =
                new IngestReviewDocument(messageId)
                {
                    CorrelationId = correlationId
                };
            _mockConsumeContext.SetupGet(x => x.Message).Returns(fakeReviewQuestionMessage);
            // DB Context Setup
            _mockDocGenerationDbContext
                .Setup(ctx => ctx.ReviewInstances)
                .Returns(_docGenerationDbContext.ReviewInstances);

            var consumer =
                new IngestReviewDocumentConsumer
                (
                    _docGenerationDbContext,
                    _fakeRepository,
                    _mockAzureFileHelper.Object,
                    _fakeLogger
                );

            // Act
            await consumer.Consume(_mockConsumeContext.Object);

            // Assert
            _mockDocGenerationDbContext
                .Verify(ctx => ctx.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async void Consume_CorrelatedReviewInstance_MessagesArePublished()
        {
            // Arrange
            // Ingest Review Document Message
            var messageId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var fakeReviewQuestionMessage =
                new IngestReviewDocument(messageId)
                {
                    CorrelationId = correlationId
                };
            _mockConsumeContext.SetupGet(x => x.Message).Returns(fakeReviewQuestionMessage);
            // AzureFileHelper
            _mockAzureFileHelper.Setup(fh => fh.GetFileAsStreamFromFullBlobUrlAsync(It.IsAny<string>())).CallBase();
            // BlobClient setup
            _mockBlobClient
                .Setup(client => client
                    .OpenReadAsync(
                        It.IsAny<long>(),
                        It.IsAny<int?>(),
                        It.IsAny<BlobRequestConditions>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockStream.Object);
            // DB Context setup
            var reviewDefinitionId = Guid.NewGuid();
            var fakeReviewDefinition = new ReviewDefinition()
            {
                Id = reviewDefinitionId,
                Title = "title",
                ReviewQuestions = []
            };
            _docGenerationDbContext.ReviewDefinitions.Add(fakeReviewDefinition);
            var exportedLinkId = Guid.NewGuid();
            var fakeExportedLink = new ExportedDocumentLink()
            {
                Id = exportedLinkId,
                MimeType = string.Empty,
                AbsoluteUrl = "http://microsoft.com/test",
                BlobContainer = string.Empty,
                FileName = string.Empty
            };
            _docGenerationDbContext.ExportedDocumentLinks.Add(fakeExportedLink);
            var fakeReviewInstance = new ReviewInstance()
            {
                Id = correlationId,
                ReviewDefinition = fakeReviewDefinition,
                ReviewDefinitionId = reviewDefinitionId,
                ExportedLinkId = exportedLinkId,
                ExportedDocumentLink = fakeExportedLink
            };
            _docGenerationDbContext.ReviewInstances.Add(fakeReviewInstance);
            _docGenerationDbContext.SaveChanges();

            var consumer =
                new IngestReviewDocumentConsumer
                (
                    _docGenerationDbContext,
                    _fakeRepository,
                    _mockAzureFileHelper.Object,
                    _fakeLogger
                );

            // Act
            await consumer.Consume(_mockConsumeContext.Object);
            string expectedQuestionMessage = "SYSTEM:TotalNumberOfQuestions=0";
            string expectedRetrievalMessage = "File being retrieved for analysis...";

            // Assert
            _mockConsumeContext.Verify(
                ctx => ctx.Publish(
                    It.Is<BackendProcessingMessageGenerated>(message =>
                        message.CorrelationId == correlationId &&
                        message.Message == expectedQuestionMessage),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _mockConsumeContext.Verify(
                ctx => ctx.Publish(
                    It.Is<BackendProcessingMessageGenerated>(message =>
                        message.CorrelationId == correlationId &&
                        message.Message == expectedRetrievalMessage),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _mockConsumeContext.Verify(
                ctx => ctx.Publish(
                    It.Is<ReviewDocumentIngested>(message =>
                        message.CorrelationId == correlationId &&
                        message.ExportedDocumentLinkId == exportedLinkId &&
                        message.TotalNumberOfQuestions == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Cleanup
            _docGenerationDbContext.ReviewInstances.Remove(fakeReviewInstance);
            _docGenerationDbContext.ExportedDocumentLinks.Remove(fakeExportedLink);
            _docGenerationDbContext.ReviewDefinitions.Remove(fakeReviewDefinition);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async void Consume_CorrelatedReviewInstanceFileStreamNull_FileRetrieveMessageNotPublished()
        {
            // Arrange
            // Ingest Review Document Message
            var messageId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var fakeReviewQuestionMessage =
                new IngestReviewDocument(messageId)
                {
                    CorrelationId = correlationId
                };
            _mockConsumeContext.SetupGet(x => x.Message).Returns(fakeReviewQuestionMessage);
            // DB Context setup
            var reviewDefinitionId = Guid.NewGuid();
            var fakeReviewDefinition = new ReviewDefinition()
            {
                Id = reviewDefinitionId,
                Title = "title",
                ReviewQuestions = []
            };
            _docGenerationDbContext.ReviewDefinitions.Add(fakeReviewDefinition);
            var exportedLinkId = Guid.NewGuid();
            var fakeExportedLink = new ExportedDocumentLink()
            {
                Id = exportedLinkId,
                MimeType = string.Empty,
                AbsoluteUrl = "http://microsoft.com/test",
                BlobContainer = string.Empty,
                FileName = string.Empty
            };
            _docGenerationDbContext.ExportedDocumentLinks.Add(fakeExportedLink);
            var fakeReviewInstance = new ReviewInstance()
            {
                Id = correlationId,
                ReviewDefinition = fakeReviewDefinition,
                ReviewDefinitionId = reviewDefinitionId,
                ExportedLinkId = exportedLinkId,
                ExportedDocumentLink = fakeExportedLink
            };
            _docGenerationDbContext.ReviewInstances.Add(fakeReviewInstance);
            _docGenerationDbContext.SaveChanges();

            var consumer =
                new IngestReviewDocumentConsumer
                (
                    _docGenerationDbContext,
                    _fakeRepository,
                    _mockAzureFileHelper.Object,
                    _fakeLogger
                );

            // Act
            await consumer.Consume(_mockConsumeContext.Object);
            string expectedQuestionMessage = "SYSTEM:TotalNumberOfQuestions=0";
            string expectedRetrievalMessage = "File being retrieved for analysis...";

            // Assert
            _mockConsumeContext.Verify(
                ctx => ctx.Publish(
                    It.Is<BackendProcessingMessageGenerated>(message =>
                        message.CorrelationId == correlationId &&
                        message.Message == expectedQuestionMessage),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _mockConsumeContext.Verify(
                ctx => ctx.Publish(
                    It.Is<BackendProcessingMessageGenerated>(message =>
                        message.CorrelationId == correlationId &&
                        message.Message == expectedRetrievalMessage),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            // Cleanup
            _docGenerationDbContext.ReviewInstances.Remove(fakeReviewInstance);
            _docGenerationDbContext.ExportedDocumentLinks.Remove(fakeExportedLink);
            _docGenerationDbContext.ReviewDefinitions.Remove(fakeReviewDefinition);
            _docGenerationDbContext.SaveChanges();
        }
    }
}