using AutoMapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;
using Xunit;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.Review.Tests
{
    public sealed class DistributeReviewQuestionsConsumerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();

        public DocGenerationDbContext DocGenerationDbContext { get; }

        public DistributeReviewQuestionsConsumerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }

        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class DistributeReviewQuestionsConsumerTests(DistributeReviewQuestionsConsumerFixture fixture)
        : IClassFixture<DistributeReviewQuestionsConsumerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext = fixture.DocGenerationDbContext;
        private readonly IMapper _mapper =
            new Mapper(new MapperConfiguration(cfg => cfg.AddProfile<ReviewInfoProfile>()));

        // Default Fakes
        private readonly ILogger<DistributeReviewQuestionsConsumer> _fakeLogger =
            new Mock<ILogger<DistributeReviewQuestionsConsumer>>().Object;
        private readonly IReviewKernelMemoryRepository _fakeRepository =
            new Mock<IReviewKernelMemoryRepository>().Object;
        private readonly Mock<ConsumeContext<DistributeReviewQuestions>> _mockConsumeContext = new();

        [Fact]
        public async void Consume_NoCorrelatedReviewInstances_DoesNothing()
        {
            // Arrange
            // Distribute Review Question Message
            var messageId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var fakeReviewQuestionMessage =
                new DistributeReviewQuestions(messageId)
                {
                    CorrelationId = correlationId
                };
            _mockConsumeContext.SetupGet(x => x.Message).Returns(fakeReviewQuestionMessage);
            // Mapper verify setup
            var _mockMapper = new Mock<IMapper>();
            _mockMapper
                .Setup(x => x.Map<List<ReviewQuestionInfo>>(It.IsAny<List<ReviewQuestionInfo>>()))
                .Verifiable();

            var unitUnderTest =
                new DistributeReviewQuestionsConsumer(
                    _docGenerationDbContext,
                    _fakeLogger,
                    _fakeRepository,
                    _mockMapper.Object);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);

            // Assert
            _mockMapper.Verify(
                x => x.Map<List<ReviewQuestionInfo>>(It.IsAny<List<ReviewQuestionInfo>>()),
                Times.Never);
        }

        [Fact]
        public async void Consume_CorrelatedReviewInstanceNoReviewQuestions_DoesNothing()
        {
            // Arrange
            // Distribute Review Question Message
            var messageId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var fakeReviewQuestionMessage =
                new DistributeReviewQuestions(messageId)
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
                AbsoluteUrl = string.Empty,
                BlobContainer = string.Empty,
                FileName = string.Empty
            };
            _docGenerationDbContext.ExportedDocumentLinks.Add(fakeExportedLink);
            var fakeReviewInstance = new ReviewInstance()
            {
                Id = correlationId,
                ReviewDefinition = fakeReviewDefinition,
                ReviewDefinitionId = reviewDefinitionId,
                ExportedLinkId = exportedLinkId
            };
            _docGenerationDbContext.ReviewInstances.Add(fakeReviewInstance);
            _docGenerationDbContext.SaveChanges();

            // Mapper verify setup
            var _mockMapper = new Mock<IMapper>();
            _mockMapper
                .Setup(x => x.Map<List<ReviewQuestionInfo>>(It.IsAny<List<ReviewQuestionInfo>>()))
                .Verifiable();
            var unitUnderTest =
                new DistributeReviewQuestionsConsumer(
                    _docGenerationDbContext,
                    _fakeLogger,
                    _fakeRepository,
                    _mockMapper.Object);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);

            // Assert
            _mockMapper.Verify(
                x => x.Map<List<ReviewQuestionInfo>>(It.IsAny<List<ReviewQuestionInfo>>()),
                Times.Never);

            // Clean up
            _docGenerationDbContext.ReviewInstances.Remove(fakeReviewInstance);
            _docGenerationDbContext.ExportedDocumentLinks.Remove(fakeExportedLink);
            _docGenerationDbContext.ReviewDefinitions.Remove(fakeReviewDefinition);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async void Consume_CorrelatedReviewInstanceWithReviewQuestions_PublishesAnswerReviewQuestionForEachQuestion()
        {
            // Arrange
            // Distribute Review Question Message
            var messageId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var fakeReviewQuestionMessage =
                new DistributeReviewQuestions(messageId)
                {
                    CorrelationId = correlationId
                };
            _mockConsumeContext.SetupGet(x => x.Message).Returns(fakeReviewQuestionMessage);
            // DB Context setup
            var reviewQuestion1Id = Guid.NewGuid();
            var fakeReviewQuestion1 = new ReviewQuestion()
            {
                Id = reviewQuestion1Id,
                Question = "question1"
            };
            var reviewQuestion2Id = Guid.NewGuid();
            var fakeReviewQuestion2 = new ReviewQuestion()
            {
                Id = reviewQuestion2Id,
                Question = "question2"
            };
            var reviewQuestionList = new List<ReviewQuestion>()
            {
                fakeReviewQuestion1,
                fakeReviewQuestion2
            };
            _docGenerationDbContext.ReviewQuestions.AddRange(reviewQuestionList);
            var reviewDefinitionId = Guid.NewGuid();
            var fakeReviewDefinition = new ReviewDefinition()
            {
                Id = reviewDefinitionId,
                Title = "title",
                ReviewQuestions = [fakeReviewQuestion1, fakeReviewQuestion2]
            };
            _docGenerationDbContext.ReviewDefinitions.Add(fakeReviewDefinition);
            var exportedLinkId = Guid.NewGuid();
            var fakeExportedLink = new ExportedDocumentLink()
            {
                Id = exportedLinkId,
                MimeType = string.Empty,
                AbsoluteUrl = string.Empty,
                BlobContainer = string.Empty,
                FileName = string.Empty
            };
            _docGenerationDbContext.ExportedDocumentLinks.Add(fakeExportedLink);
            var fakeReviewInstance = new ReviewInstance()
            {
                Id = correlationId,
                ReviewDefinition = fakeReviewDefinition,
                ReviewDefinitionId = reviewDefinitionId,
                ExportedLinkId = exportedLinkId
            };
            _docGenerationDbContext.ReviewInstances.Add(fakeReviewInstance);
            _docGenerationDbContext.SaveChanges();

            // Mapper verify setup
            var unitUnderTest =
                new DistributeReviewQuestionsConsumer(
                    _docGenerationDbContext,
                    _fakeLogger,
                    _fakeRepository,
                    _mapper);

            // Act
            await unitUnderTest.Consume(_mockConsumeContext.Object);

            // Assert
            _mockConsumeContext.Verify(
                x => x.Publish(
                    It.Is<AnswerReviewQuestion>(message =>
                        message.CorrelationId == correlationId &&
                        message.ReviewQuestion.Id == reviewQuestion1Id &&
                        message.TotalQuestions == reviewQuestionList.Count),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _mockConsumeContext.Verify(
                x => x.Publish(
                    It.Is<AnswerReviewQuestion>(message =>
                        message.CorrelationId == correlationId &&
                        message.ReviewQuestion.Id == reviewQuestion2Id &&
                        message.TotalQuestions == reviewQuestionList.Count),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Clean up
            _docGenerationDbContext.ReviewInstances.Remove(fakeReviewInstance);
            _docGenerationDbContext.ExportedDocumentLinks.Remove(fakeExportedLink);
            _docGenerationDbContext.ReviewDefinitions.Remove(fakeReviewDefinition);
            _docGenerationDbContext.ReviewQuestions.RemoveRange(reviewQuestionList);
            _docGenerationDbContext.SaveChanges();
        }
    }
}