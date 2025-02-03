using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.Tests
{
    public sealed class CreateGeneratedDocumentConsumerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();

        public DocGenerationDbContext DocGenerationDbContext { get; }

        public CreateGeneratedDocumentConsumerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }

        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public sealed class CreateGeneratedDocumentConsumerTests
        : IClassFixture<CreateGeneratedDocumentConsumerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;

        // Default Fakes
        private readonly ILogger<CreateGeneratedDocumentConsumer> _fakeLogger =
            new Mock<ILogger<CreateGeneratedDocumentConsumer>>().Object;
        private readonly DbContextOptions<DocGenerationDbContext> _fakeDbContextOptions = new();
        private readonly CreateGeneratedDocument _fakeMessage = new(Guid.NewGuid())
        {
            OriginalDTO = new GenerateDocumentDTO()
            {
                AuthorOid = Guid.Empty.ToString()
            }
        };

        // Default Mocks
        private readonly Mock<ConsumeContext<CreateGeneratedDocument>> _mockConsumeContext = new();
        private readonly Mock<DocGenerationDbContext> _mockDocGenerationDbContext;

        public CreateGeneratedDocumentConsumerTests(CreateGeneratedDocumentConsumerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _mockDocGenerationDbContext = new Mock<DocGenerationDbContext>(_fakeDbContextOptions);
            _mockConsumeContext.Setup(c => c.Message).Returns(_fakeMessage);
        }

        [Fact]
        public async void Consume_GeneratedDocumentDoesNotExist_NothingIsRemoved()
        {
            // Arrange
            // Mocks
            _mockDocGenerationDbContext.Setup(ctx => ctx.GeneratedDocuments.FindAsync(It.IsAny<Guid>()));
            _mockDocGenerationDbContext.SetupGet(ctx => ctx.DocumentMetadata)
                .Returns(_docGenerationDbContext.DocumentMetadata);

            var unitUnderTest = new CreateGeneratedDocumentConsumer(_fakeLogger, _mockDocGenerationDbContext.Object);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);

            // Assert
            _mockDocGenerationDbContext.Verify(
                d => d.GeneratedDocuments.Remove(It.IsAny<GeneratedDocument>()), Times.Never);
            _mockDocGenerationDbContext.Verify(
                d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async void Consume_GeneratedDocumentExists_ExistingDocumentIsRemoved()
        {
            // Arrange
            // Mocks
            // Add records to the context
            var fakeGeneratedDocument = new GeneratedDocument() { Title = "title", Id = _fakeMessage.CorrelationId };
            _docGenerationDbContext.Add<GeneratedDocument>(fakeGeneratedDocument);
            _docGenerationDbContext.SaveChanges();
            // Mock DocGenerationDbContext with passthrough to real context for GeneratedDocuments.FindAsync
            _mockDocGenerationDbContext.Setup(ctx => ctx.GeneratedDocuments.FindAsync(_fakeMessage.CorrelationId))
                .Returns(_docGenerationDbContext.GeneratedDocuments.FindAsync(_fakeMessage.CorrelationId));
            _mockDocGenerationDbContext.SetupGet(ctx => ctx.DocumentMetadata)
                .Returns(_docGenerationDbContext.DocumentMetadata);
            var unitUnderTest = new CreateGeneratedDocumentConsumer(_fakeLogger, _mockDocGenerationDbContext.Object);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);

            // Assert
            _mockDocGenerationDbContext.Verify(d => d.GeneratedDocuments.Remove(fakeGeneratedDocument), Times.Once);
            _mockDocGenerationDbContext.Verify(
                d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

            // Cleanup
            _docGenerationDbContext.Remove<GeneratedDocument>(fakeGeneratedDocument);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async void Consume_NewDocument_PublishesGeneratedDocumentCreatedWithMetadataId()
        {
            // Arrange
            // Output variables for callbacks
            GeneratedDocumentCreated publishedMessage = null!;
            DocumentMetadata documentMetadataParameter = null!;
            // Mocks
            _mockConsumeContext
                .Setup(ctx => ctx.Publish(It.IsAny<GeneratedDocumentCreated>(), It.IsAny<CancellationToken>()))
                .Callback<GeneratedDocumentCreated, CancellationToken>((message, token) => publishedMessage = message);
            // Mock DocGenerationDbContext
            _mockDocGenerationDbContext.Setup(ctx => ctx.GeneratedDocuments.FindAsync(It.IsAny<Guid>()));
            _mockDocGenerationDbContext
                .Setup(ctx =>
                    ctx.DocumentMetadata.AddAsync(It.IsAny<DocumentMetadata>(), It.IsAny<CancellationToken>()))
                .Callback<DocumentMetadata, CancellationToken>((doc, token) => documentMetadataParameter = doc);

            var unitUnderTest = new CreateGeneratedDocumentConsumer(_fakeLogger, _mockDocGenerationDbContext.Object);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);

            // Assert
            Assert.Equal(_fakeMessage.CorrelationId, publishedMessage.CorrelationId);
            Assert.Equal(documentMetadataParameter.Id, publishedMessage.MetaDataId);
        }
    }
}