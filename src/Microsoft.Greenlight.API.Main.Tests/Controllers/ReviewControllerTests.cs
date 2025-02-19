using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Testing.SQLite;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class ReviewControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public ReviewControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }

    public class ReviewControllerTests : IClassFixture<ReviewControllerFixture>
    {
        private readonly DocGenerationDbContext _docGenerationDbContext;
        private readonly IMapper _mapper;

        private const string oldTitle = "Old Title";
        private const string newTitle = "New Title";
        private const string reviewTitle = "Review Title";
        private const string updatedReviewTitle = "Updated Review Title";
        private const string existingQuestion = "Existing Question";
        private const string newQuestion = "New Question";

        public ReviewControllerTests(ReviewControllerFixture fixture)
        {
            _docGenerationDbContext = fixture.DocGenerationDbContext;
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<ReviewInfoProfile>()).CreateMapper();
        }

        [Fact]
        public async Task GetReviewById_WhenReviewDoesNotExist_ReturnsNotFoundResult()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var _controller = new ReviewController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.GetReviewById(reviewId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task UpdateReview_WhenExistingReviewWithUpdates_UpdatesToDatabaseCorrectly()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var existingReview = new ReviewDefinition { Id = reviewId, Title = oldTitle };
            _docGenerationDbContext.ReviewDefinitions.Add(existingReview);

            var changeRequest = new ReviewChangeRequest
            {
                ReviewDefinition = new ReviewDefinitionInfo 
                { 
                    Id = reviewId, 
                    Title = newTitle
                },
                ChangedOrAddedQuestions = [],
                DeletedQuestions = []
            };
            _docGenerationDbContext.SaveChanges();
            var _controller = new ReviewController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.UpdateReview(reviewId, changeRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<ReviewDefinitionInfo>(okResult.Value);
            Assert.Equal(changeRequest.ReviewDefinition.Id, returnValue.Id);
            Assert.Equal(changeRequest.ReviewDefinition.Title, returnValue.Title);

            // Cleanup
            _docGenerationDbContext.ReviewDefinitions.Remove(existingReview);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task UpdateReview_WhenNewQuestionWithIdDoesNotExist_UpdatesToDatabaseCorrectly()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var existingReview = new ReviewDefinition { Id = reviewId, Title = oldTitle };
            _docGenerationDbContext.ReviewDefinitions.Add(existingReview);

            var newQuestionId = Guid.NewGuid();
            var newQuestionInfo = new ReviewQuestionInfo
            { 
                Id = newQuestionId, 
                ReviewId = reviewId, 
                Question = ReviewControllerTests.newQuestion
            };

            var changeRequest = new ReviewChangeRequest
            {
                ReviewDefinition = new ReviewDefinitionInfo { Id = reviewId, Title = newTitle },
                ChangedOrAddedQuestions = [newQuestionInfo],
                DeletedQuestions = []
            };
            _docGenerationDbContext.SaveChanges();
            var _controller = new ReviewController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.UpdateReview(reviewId, changeRequest);

            // Assert
            var addedQuestion = await _docGenerationDbContext.ReviewQuestions
                .FirstOrDefaultAsync(q => q.Id == newQuestionId);
            Assert.NotNull(addedQuestion);
            Assert.Equal(newQuestionId, addedQuestion.Id);

            // Cleanup
            _docGenerationDbContext.ReviewDefinitions.Remove(existingReview);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task UpdateReview_WhenNewQuestionWithoutId_UpdatesToDatabaseCorrectly()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var existingReview = new ReviewDefinition { Id = reviewId, Title = oldTitle};
            _docGenerationDbContext.ReviewDefinitions.Add(existingReview);
            _docGenerationDbContext.SaveChanges();

            var newQuestionInfo = new ReviewQuestionInfo 
            { 
                Id = Guid.Empty, 
                ReviewId = reviewId, 
                Question = newQuestion
            };
            var changeRequest = new ReviewChangeRequest
            {
                ReviewDefinition = new ReviewDefinitionInfo { Id = reviewId, Title = newTitle },
                ChangedOrAddedQuestions = [newQuestionInfo],
                DeletedQuestions = []
            };
            var _controller = new ReviewController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.UpdateReview(reviewId, changeRequest);

            // Assert
            var addedQuestion = await _docGenerationDbContext.ReviewQuestions.
                FirstOrDefaultAsync(q => q.Question == newQuestion);
            Assert.NotNull(addedQuestion);
            Assert.Equal(newQuestion, addedQuestion.Question);
            Assert.Equal(reviewId, addedQuestion.ReviewId);

            // Cleanup
            _docGenerationDbContext.ReviewDefinitions.Remove(existingReview);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task UpdateReview_WhenDeletingExistingQuestion_RemovesFromDatabaseCorrectly()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var questionId = Guid.NewGuid();
            var existingReview = new ReviewDefinition { Id = reviewId, Title = reviewTitle };
            _docGenerationDbContext.ReviewDefinitions.Add(existingReview);

            var existingQuestionEntity = new ReviewQuestion
            {
                Id = questionId,
                ReviewId = reviewId,
                Question = existingQuestion
            };
            _docGenerationDbContext.ReviewQuestions.Add(existingQuestionEntity);
            var changeRequest = new ReviewChangeRequest
            {
                ReviewDefinition = new ReviewDefinitionInfo
                {
                    Id = reviewId,
                    Title = updatedReviewTitle
                },
                ChangedOrAddedQuestions = new List<ReviewQuestionInfo>(),
                DeletedQuestions = new List<ReviewQuestionInfo> { new() { Id = questionId, ReviewId = reviewId, Question = string.Empty } }
            };

            _docGenerationDbContext.SaveChanges();
            var _controller = new ReviewController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.UpdateReview(reviewId, changeRequest);

            // Assert
            var deletedQuestion = await _docGenerationDbContext.ReviewQuestions
                .FirstOrDefaultAsync(q => q.Id == questionId);
            Assert.Null(deletedQuestion);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<ReviewDefinitionInfo>(okResult.Value);
            Assert.Equal(changeRequest.ReviewDefinition.Id, returnValue.Id);
            Assert.Equal(changeRequest.ReviewDefinition.Title, returnValue.Title);

            // Cleanup
            _docGenerationDbContext.ReviewDefinitions.Remove(existingReview);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task DeleteReview_WhenReviewIsNull_ReturnsNotFound()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var _controller = new ReviewController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.DeleteReview(reviewId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
