using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Mappings;
using Xunit;
using Assert = Xunit.Assert;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Tests.Mappings
{
    public class ReviewQuestionAnswerInfoProfileTests
    {
        private readonly IMapper _mapper;

        public ReviewQuestionAnswerInfoProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<ReviewQuestionAnswerInfoProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Should_Map_ReviewQuestionAnswer_To_ReviewQuestionAnswerInfo()
        {
            // Arrange
            var reviewQuestion = new ReviewQuestion
            {
                Id = Guid.NewGuid(),
                Question = "Sample Question",
                QuestionType = ReviewQuestionType.Question
            };

            var reviewQuestionAnswer = new ReviewQuestionAnswer
            {
                OriginalReviewQuestion = reviewQuestion,
                OriginalReviewQuestionText = "Original Question Text",
                OriginalReviewQuestionType = ReviewQuestionType.Requirement,
                FullAiAnswer = "AI Answer"
            };

            // Act
            var result = _mapper.Map<ReviewQuestionAnswerInfo>(reviewQuestionAnswer);

            // Assert
            Assert.Equal(
                reviewQuestionAnswer.OriginalReviewQuestionText, 
                result.Question);
            Assert.Equal(reviewQuestion.Id, result.ReviewQuestionId);
            Assert.Equal(reviewQuestionAnswer.FullAiAnswer, result.AiAnswer);
            Assert.Equal(ReviewQuestionType.Requirement, result.QuestionType);
        }

        [Fact]
        public void Should_Map_ReviewQuestionAnswerInfo_To_ReviewQuestionAnswer()
        {
            // Arrange
            var reviewQuestionAnswerInfo = new ReviewQuestionAnswerInfo
            {
                AiAnswer = "AI Answer",
                QuestionType = ReviewQuestionType.Requirement
            };

            // Act
            var result = _mapper.Map<ReviewQuestionAnswer>(reviewQuestionAnswerInfo);

            // Assert
            Assert.Equal("AI Answer", result.FullAiAnswer);
            Assert.Equal(ReviewQuestionType.Requirement, result.OriginalReviewQuestionType);
        }
    }
}
