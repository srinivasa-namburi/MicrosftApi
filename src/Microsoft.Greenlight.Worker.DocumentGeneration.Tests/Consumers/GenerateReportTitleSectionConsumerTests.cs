using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;
using System.Text.Json;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.Tests
{
    public sealed class GenerateReportTitleSectionConsumerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();

        public DocGenerationDbContext DocGenerationDbContext { get; }

        public GenerateReportTitleSectionConsumerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }

        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class GenerateReportTitleSectionConsumerTests
        : IClassFixture<GenerateReportTitleSectionConsumerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;

        // Default Fakes
        private readonly ILogger<GenerateReportTitleSectionConsumer> _fakeLogger =
            new Mock<ILogger<GenerateReportTitleSectionConsumer>>().Object;
        private readonly DbContextOptions<DocGenerationDbContext> _fakeDbContextOptions = new();

        // Default Mocks
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint = new();
        private readonly Mock<IKeyedServiceProvider> _mockServiceProvider = new();
        private readonly Mock<ConsumeContext<GenerateReportTitleSection>> _mockConsumeContext = new();
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory = new();
        private readonly Mock<IDocumentProcessInfoService> _mockDocumentProcessInfoService = new();
        private readonly Mock<IServiceScope> _mockScope = new();
        private readonly Mock<IBodyTextGenerator> _mockBodyTextGenerator = new();
        private readonly Mock<DocumentProcessInfo> _mockDocumentProcessInfo = new();
        private readonly Mock<DocGenerationDbContext> _mockDocGenerationDbContext;

        public GenerateReportTitleSectionConsumerTests(GenerateReportTitleSectionConsumerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _mockDocGenerationDbContext = new Mock<DocGenerationDbContext>(_fakeDbContextOptions);
            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(IBodyTextGenerator))).Returns(_mockBodyTextGenerator.Object);
            _mockServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IBodyTextGenerator), It.IsAny<string>()))
                .Returns(_mockBodyTextGenerator.Object);

            _mockScope.SetupGet(scope => scope.ServiceProvider).Returns(_mockServiceProvider.Object);
            _mockScopeFactory.Setup(factory => factory.CreateScope()).Returns(_mockScope.Object);
            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(_mockScopeFactory.Object);
            _mockDocumentProcessInfo.SetupGet(info => info.ShortName).Returns("Review");
            _mockDocumentProcessInfoService
                .Setup(infoService => infoService.GetDocumentProcessInfoByShortNameAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(_mockDocumentProcessInfo.Object)!);
            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(IDocumentProcessInfoService)))
                .Returns(_mockDocumentProcessInfoService.Object);
        }

        [Fact]
        public async void Consume_InvalidContent_ThrowsJsonException()
        {
            // Arrange
            // Create a fake report section with invalid content
            var fakeReportSection = new GenerateReportTitleSection(Guid.NewGuid())
            {
                ContentNodeJson = "Invalid",
                DocumentOutlineJson = "[]"
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(fakeReportSection);

            var unitUnderTest =
                new GenerateReportTitleSectionConsumer(
                    _fakeLogger,
                    _docGenerationDbContext,
                    _mockServiceProvider.Object,
                    _mockPublishEndpoint.Object);

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(
                async () => await unitUnderTest.Consume(_mockConsumeContext.Object));
        }

        [Fact]
        public async void Consume_InvalidOutline_ThrowsJsonException()
        {
            // Arrange
            // Create a fake report section with invalid content
            var fakeReportSection = new GenerateReportTitleSection(Guid.NewGuid())
            {
                ContentNodeJson = "{}",
                DocumentOutlineJson = "Invalid"
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(fakeReportSection);

            var unitUnderTest =
                new GenerateReportTitleSectionConsumer(
                    _fakeLogger,
                    _docGenerationDbContext,
                    _mockServiceProvider.Object,
                    _mockPublishEndpoint.Object);

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(
                async () => await unitUnderTest.Consume(_mockConsumeContext.Object));
        }

        [Fact]
        public async void Consume_TrackedDocumentDoesntExist_DoesNothing()
        {
            // Arrange
            // Create a fake report section with valid content
            var fakeReportSection = new GenerateReportTitleSection(Guid.NewGuid())
            {
                ContentNodeJson = "{}",
                DocumentOutlineJson = "[]"
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(fakeReportSection);

            // DB Context setup
            _mockDocGenerationDbContext
                .SetupGet(ctx => ctx.GeneratedDocuments)
                .Returns(_docGenerationDbContext.GeneratedDocuments);

            var unitUnderTest =
                new GenerateReportTitleSectionConsumer(
                    _fakeLogger,
                    _mockDocGenerationDbContext.Object,
                    _mockServiceProvider.Object,
                    _mockPublishEndpoint.Object);
            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);

            // Assert
            _mockDocGenerationDbContext.Verify(ctx => ctx.ContentNodes, Times.Never);
        }

        [Fact]
        public async void Consume_ChildContentNodesExist_OnlyBodyTextChildrenAreRemoved()
        {
            // Arrange
            // Create a fake report section with valid content
            var messageId = Guid.NewGuid();
            var contentNodeid = Guid.NewGuid();
            var fakeReportSection = new GenerateReportTitleSection(messageId)
            {
                ContentNodeJson = $"{{\"Id\":\"{contentNodeid}\"}}",
                DocumentOutlineJson = "[]"
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(fakeReportSection);
            // IBodyTextGenerator setup
            var contentNodeSystemItemId = Guid.NewGuid();
            _mockBodyTextGenerator
                .Setup(gen =>
                    gen.GenerateBodyText(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<ContentNode?>()))
                .Returns(Task.FromResult(new List<ContentNode>() { }));
            // DB Context setup
            var fakeSourceReference = new DocumentLibrarySourceReferenceItem();
            _docGenerationDbContext.SourceReferenceItems.Add(fakeSourceReference);
            var fakeContentNodeSystemItem = new ContentNodeSystemItem();
            fakeContentNodeSystemItem.SourceReferences.Add(fakeSourceReference);
            _docGenerationDbContext.ContentNodeSystemItems.Add(fakeContentNodeSystemItem);
            var bodyNodeId = Guid.NewGuid();
            var fakeBodyNode =
                new ContentNode()
                {
                    Id = bodyNodeId,
                    ContentNodeSystemItem = fakeContentNodeSystemItem,
                    Type = ContentNodeType.BodyText
                };
            var headingNodeId = Guid.NewGuid();
            var fakeHeadingNode =
                new ContentNode()
                {
                    Id = headingNodeId,
                    ContentNodeSystemItem = fakeContentNodeSystemItem,
                    Type = ContentNodeType.Heading
                };
            _docGenerationDbContext.ContentNodes.Add(fakeBodyNode);
            _docGenerationDbContext.ContentNodes.Add(fakeHeadingNode);
            var fakeContentNode = new ContentNode() { Id = contentNodeid };
            fakeContentNode.Children.Add(fakeBodyNode);
            fakeContentNode.Children.Add(fakeHeadingNode);
            _docGenerationDbContext.ContentNodes.Add(fakeContentNode);
            var fakeGeneratedDocument =
                new GeneratedDocument() { DocumentProcess = String.Empty, Id = messageId, Title = String.Empty };
            _docGenerationDbContext.GeneratedDocuments.Add(fakeGeneratedDocument);
            _docGenerationDbContext.SaveChanges();

            var unitUnderTest =
                new GenerateReportTitleSectionConsumer(
                    _fakeLogger,
                    _docGenerationDbContext,
                    _mockServiceProvider.Object,
                    _mockPublishEndpoint.Object);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);

            // Assert
            Assert.DoesNotContain(
                _docGenerationDbContext.ContentNodes.Find(contentNodeid)!.Children,
                ch => ch.Id == bodyNodeId);
            Assert.Contains(
                _docGenerationDbContext.ContentNodes.Find(contentNodeid)!.Children,
                ch => ch.Id == headingNodeId);

            // Cleanup
            _docGenerationDbContext.GeneratedDocuments.Remove(fakeGeneratedDocument);
            _docGenerationDbContext.ContentNodes.RemoveRange(fakeContentNode);
            _docGenerationDbContext.ContentNodeSystemItems.Remove(fakeContentNodeSystemItem);
            _docGenerationDbContext.SourceReferenceItems.Remove(fakeSourceReference);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async void Consume_ContentNodeAndTrackedDocumentExist_ExistingContentNodeUpdated()
        {
            // Arrange
            // Create a fake report section with valid content
            var messageId = Guid.NewGuid();
            var contentNodeid = Guid.NewGuid();
            var fakeReportSection = new GenerateReportTitleSection(messageId)
            {
                ContentNodeJson = $"{{\"Id\":\"{contentNodeid}\"}}",
                DocumentOutlineJson = "[]"
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(fakeReportSection);
            // IBodyTextGenerator setup
            var contentNodeSystemItemId = Guid.NewGuid();
            _mockBodyTextGenerator
                .Setup(gen =>
                    gen.GenerateBodyText(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<ContentNode?>()))
                .Returns(
                    Task.FromResult(
                        new List<ContentNode>()
                        {
                            new()
                            {
                                ContentNodeSystemItemId = contentNodeSystemItemId,
                                ContentNodeSystemItem = new() { Id = contentNodeSystemItemId }
                            }
                        }));
            // DB Context setup
            var fakeSourceReference = new DocumentLibrarySourceReferenceItem();
            _docGenerationDbContext.SourceReferenceItems.Add(fakeSourceReference);
            var fakeContentNodeSystemItem = new ContentNodeSystemItem();
            fakeContentNodeSystemItem.SourceReferences.Add(fakeSourceReference);
            _docGenerationDbContext.ContentNodeSystemItems.Add(fakeContentNodeSystemItem);
            var bodyNodeId = Guid.NewGuid();
            var fakeBodyNode =
                new ContentNode()
                {
                    Id = bodyNodeId,
                    ContentNodeSystemItem = fakeContentNodeSystemItem,
                    Type = ContentNodeType.BodyText
                };
            var headingNodeId = Guid.NewGuid();
            var fakeHeadingNode =
                new ContentNode()
                {
                    Id = headingNodeId,
                    ContentNodeSystemItem = fakeContentNodeSystemItem,
                    Type = ContentNodeType.Heading
                };
            _docGenerationDbContext.ContentNodes.Add(fakeBodyNode);
            _docGenerationDbContext.ContentNodes.Add(fakeHeadingNode);
            var fakeContentNode = new ContentNode() { Id = contentNodeid };
            fakeContentNode.Children.Add(fakeBodyNode);
            fakeContentNode.Children.Add(fakeHeadingNode);
            _docGenerationDbContext.ContentNodes.Add(fakeContentNode);
            var fakeGeneratedDocument =
                new GeneratedDocument() { DocumentProcess = String.Empty, Id = messageId, Title = String.Empty };
            _docGenerationDbContext.GeneratedDocuments.Add(fakeGeneratedDocument);
            _docGenerationDbContext.SaveChanges();

            var unitUnderTest =
                new GenerateReportTitleSectionConsumer(
                    _fakeLogger,
                    _docGenerationDbContext,
                    _mockServiceProvider.Object,
                    _mockPublishEndpoint.Object);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);
            var actual = _docGenerationDbContext.ContentNodes.Find(contentNodeid);

            // Assert
            Assert.True(actual!.ContentNodeSystemItemId == contentNodeSystemItemId);
            Assert.True(actual!.ContentNodeSystemItem!.Id == contentNodeSystemItemId);

            // Cleanup
            _docGenerationDbContext.GeneratedDocuments.Remove(fakeGeneratedDocument);
            _docGenerationDbContext.ContentNodes.RemoveRange(fakeContentNode);
            _docGenerationDbContext.ContentNodeSystemItems.Remove(fakeContentNodeSystemItem);
            _docGenerationDbContext.SourceReferenceItems.Remove(fakeSourceReference);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async void Consume_ContentNodeAndTrackedDocumentExist_UpdatedNewContentNodeAdded()
        {
            // Arrange
            // Create a fake report section with valid content
            var messageId = Guid.NewGuid();
            var contentNodeid = Guid.NewGuid();
            var fakeReportSection = new GenerateReportTitleSection(messageId)
            {
                ContentNodeJson = $"{{\"Id\":\"{contentNodeid}\"}}",
                DocumentOutlineJson = "[]"
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(fakeReportSection);
            // IBodyTextGenerator setup
            var generatedContentNodeId = Guid.NewGuid();
            var contentNodeSystemItemId = Guid.NewGuid();
            _mockBodyTextGenerator
                .Setup(gen =>
                    gen.GenerateBodyText(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<ContentNode?>()))
                .Returns(
                    Task.FromResult(
                        new List<ContentNode>()
                        {
                            new()
                            {
                                Id = generatedContentNodeId,
                                ContentNodeSystemItemId = contentNodeSystemItemId,
                                ContentNodeSystemItem = new() { Id = contentNodeSystemItemId }
                            }
                        }));
            // DB Context setup
            var fakeSourceReference = new DocumentLibrarySourceReferenceItem();
            _docGenerationDbContext.SourceReferenceItems.Add(fakeSourceReference);
            var fakeContentNodeSystemItem = new ContentNodeSystemItem();
            fakeContentNodeSystemItem.SourceReferences.Add(fakeSourceReference);
            _docGenerationDbContext.ContentNodeSystemItems.Add(fakeContentNodeSystemItem);
            var bodyNodeId = Guid.NewGuid();
            var fakeBodyNode =
                new ContentNode()
                {
                    Id = bodyNodeId,
                    ContentNodeSystemItem = fakeContentNodeSystemItem,
                    Type = ContentNodeType.BodyText
                };
            var headingNodeId = Guid.NewGuid();
            var fakeHeadingNode =
                new ContentNode()
                {
                    Id = headingNodeId,
                    ContentNodeSystemItem = fakeContentNodeSystemItem,
                    Type = ContentNodeType.Heading
                };
            _docGenerationDbContext.ContentNodes.Add(fakeBodyNode);
            _docGenerationDbContext.ContentNodes.Add(fakeHeadingNode);
            var fakeContentNode = new ContentNode() { Id = contentNodeid };
            fakeContentNode.Children.Add(fakeBodyNode);
            fakeContentNode.Children.Add(fakeHeadingNode);
            _docGenerationDbContext.ContentNodes.Add(fakeContentNode);
            var fakeGeneratedDocument =
                new GeneratedDocument() { DocumentProcess = String.Empty, Id = messageId, Title = String.Empty };
            _docGenerationDbContext.GeneratedDocuments.Add(fakeGeneratedDocument);
            _docGenerationDbContext.SaveChanges();

            var unitUnderTest =
                new GenerateReportTitleSectionConsumer(
                    _fakeLogger,
                    _docGenerationDbContext,
                    _mockServiceProvider.Object,
                    _mockPublishEndpoint.Object);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);
            var actual = _docGenerationDbContext.ContentNodes.Find(generatedContentNodeId);

            // Assert
            Assert.Null(actual!.ContentNodeSystemItemId);
            Assert.Null(actual!.ContentNodeSystemItem);
            Assert.True(actual!.ParentId == contentNodeid);

            // Cleanup
            _docGenerationDbContext.GeneratedDocuments.Remove(fakeGeneratedDocument);
            _docGenerationDbContext.ContentNodes.RemoveRange(fakeContentNode);
            _docGenerationDbContext.ContentNodeSystemItems.Remove(fakeContentNodeSystemItem);
            _docGenerationDbContext.SourceReferenceItems.Remove(fakeSourceReference);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async void Consume_ContentNodeAndTrackedDocumentExist_PublishesStateChangedEvents()
        {
            // Arrange
            // Create a fake report section with valid content
            var messageId = Guid.NewGuid();
            var contentNodeid = Guid.NewGuid();
            var fakeReportSection = new GenerateReportTitleSection(messageId)
            {
                ContentNodeJson = $"{{\"Id\":\"{contentNodeid}\"}}",
                DocumentOutlineJson = "[]"
            };
            _mockConsumeContext.Setup(c => c.Message).Returns(fakeReportSection);
            // IBodyTextGenerator setup
            var contentNodeSystemItemId = Guid.NewGuid();
            _mockBodyTextGenerator
                .Setup(gen =>
                    gen.GenerateBodyText(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<ContentNode?>()))
                .Returns(Task.FromResult(new List<ContentNode>() { }));
            // DB Context setup
            var fakeContentNode = new ContentNode() { Id = contentNodeid };
            _docGenerationDbContext.ContentNodes.Add(fakeContentNode);
            var fakeGeneratedDocument =
                new GeneratedDocument() { DocumentProcess = String.Empty, Id = messageId, Title = String.Empty };
            _docGenerationDbContext.GeneratedDocuments.Add(fakeGeneratedDocument);
            _docGenerationDbContext.SaveChanges();

            var unitUnderTest =
                new GenerateReportTitleSectionConsumer(
                    _fakeLogger,
                    _docGenerationDbContext,
                    _mockServiceProvider.Object,
                    _mockPublishEndpoint.Object);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);

            // Assert
            _mockPublishEndpoint.Verify(
                pe => pe.Publish(
                    It.Is<ContentNodeGenerationStateChanged>(
                        stateChanged =>
                            stateChanged.CorrelationId == messageId &&
                            stateChanged.ContentNodeId == contentNodeid &&
                            stateChanged.GenerationState == ContentNodeGenerationState.InProgress),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _mockPublishEndpoint.Verify(
                pe => pe.Publish(
                    It.Is<ContentNodeGenerationStateChanged>(
                        stateChanged =>
                            stateChanged.CorrelationId == messageId &&
                            stateChanged.ContentNodeId == contentNodeid &&
                            stateChanged.GenerationState == ContentNodeGenerationState.Completed),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Cleanup
            _docGenerationDbContext.GeneratedDocuments.Remove(fakeGeneratedDocument);
            _docGenerationDbContext.ContentNodes.Remove(fakeContentNode);
            _docGenerationDbContext.SaveChanges();
        }
    }
}