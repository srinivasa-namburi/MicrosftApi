using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Testing.SQLite;
using Moq;
using Microsoft.Greenlight.Shared.Mappings;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class ReviewInstanceControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public ReviewInstanceControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class ReviewInstanceControllerTests : IClassFixture<ReviewInstanceControllerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;
        private readonly IMapper _mapper;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly ReviewInstanceController _controller;

        public ReviewInstanceControllerTests(ReviewInstanceControllerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<ReviewInstanceInfoProfile>()).CreateMapper();
            _publishEndpointMock = new Mock<IPublishEndpoint>();
            _controller = new ReviewInstanceController(_docGenerationDbContext, _mapper, _publishEndpointMock.Object);
        }

        [Fact]
        public async Task GetReviewInstanceById_WhenReviewInstanceDoesNotExist_ReturnsNotFound()
        {
            // Act
            var result = await _controller.GetReviewInstanceById(Guid.NewGuid());

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetReviewInstanceAnswers_WhenReviewInstanceDoesNotExist_ReturnsNotFound()
        {
            // Act
            var result = await _controller.GetReviewInstanceAnswers(Guid.NewGuid());

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateReviewInstance_WhenReviewDefinitionIdIsInvalid_ReturnsBadRequest()
        {
            // Arrange
            var reviewInstanceInfo = new ReviewInstanceInfo
            {
                ReviewDefinitionId = Guid.NewGuid(),
                ExportedLinkId = Guid.NewGuid()
            };

            // Act
            var result = await _controller.CreateReviewInstance(reviewInstanceInfo);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateReviewInstance_WhenExportedLinkIdIsInvalid_ReturnsBadRequest()
        {
            // Arrange
            var reviewDefinition = new ReviewDefinition { Id = Guid.NewGuid(), Title = "Sample Title" };
            _docGenerationDbContext.ReviewDefinitions.Add(reviewDefinition);
            _docGenerationDbContext.SaveChanges();

            var reviewInstanceInfo = new ReviewInstanceInfo
            {
                ReviewDefinitionId = reviewDefinition.Id,
                ExportedLinkId = Guid.NewGuid()
            };

            // Act
            var result = await _controller.CreateReviewInstance(reviewInstanceInfo);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);

            // Clean up
            _docGenerationDbContext.ReviewDefinitions.Remove(reviewDefinition);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task CreateReviewInstance_WhenReviewInstanceIsCreated_UpdatesDatabaseCorrectly()
        {
            // Arrange
            var reviewDefinition = new ReviewDefinition { Id = Guid.NewGuid(), Title = "Sample Title" };
            var exportedDocumentLink = new ExportedDocumentLink
            {
                Id = Guid.NewGuid(),
                MimeType = "application/pdf",
                AbsoluteUrl = "http://example.com/document.pdf",
                BlobContainer = "documents",
                FileName = "document.pdf"
            };
            _docGenerationDbContext.ReviewDefinitions.Add(reviewDefinition);
            _docGenerationDbContext.ExportedDocumentLinks.Add(exportedDocumentLink);
            _docGenerationDbContext.SaveChanges();

            var reviewInstanceInfo = new ReviewInstanceInfo
            {
                ReviewDefinitionId = reviewDefinition.Id,
                ExportedLinkId = exportedDocumentLink.Id
            };

            // Act
            var result = await _controller.CreateReviewInstance(reviewInstanceInfo);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<ReviewInstanceInfo>(okResult.Value);
            var reviewInstanceInDb = await _docGenerationDbContext.ReviewInstances.FindAsync(returnValue.Id);
            Assert.NotNull(reviewInstanceInDb);
            Assert.Equal(reviewInstanceInfo.ReviewDefinitionId, reviewInstanceInDb.ReviewDefinitionId);
            Assert.Equal(reviewInstanceInfo.ExportedLinkId, reviewInstanceInDb.ExportedLinkId);

            // Clean up
            _docGenerationDbContext.ReviewDefinitions.Remove(reviewDefinition);
            _docGenerationDbContext.ExportedDocumentLinks.Remove(exportedDocumentLink);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task SubmitExecutionRequestForReviewInstance_WhenReviewInstanceDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var nonExistentReviewInstanceId = Guid.NewGuid();

            // Act
            var result = await _controller.SubmitExecutionRequestForReviewInstance(nonExistentReviewInstanceId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}
