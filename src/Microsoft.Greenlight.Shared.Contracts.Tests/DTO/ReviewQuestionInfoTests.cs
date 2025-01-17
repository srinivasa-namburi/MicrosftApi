using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Tests
{
    public class ReviewQuestionInfoTests
    {
        [Fact]
        public void Equals_SameProperties_ReturnsTrue()
        {
            // Arrange
            var question1 = new ReviewQuestionInfo
            {
                Id = Guid.NewGuid(),
                Question = "Is this a test question?",
                Rationale = "Test rationale",
                ReviewId = Guid.NewGuid(),
                QuestionType = ReviewQuestionType.Question
            };
            var question2 = new ReviewQuestionInfo
            {
                Id = question1.Id,
                Question = question1.Question,
                Rationale = question1.Rationale,
                ReviewId = question1.ReviewId,
                QuestionType = question1.QuestionType
            };

            // Act
            var result = question1.Equals(question2);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Equals_DifferentProperties_ReturnsFalse()
        {
            // Arrange
            var question1 = new ReviewQuestionInfo
            {
                Id = Guid.NewGuid(),
                Question = "Is this a test question?",
                Rationale = "Test rationale",
                ReviewId = Guid.NewGuid(),
                QuestionType = ReviewQuestionType.Question
            };
            var question2 = new ReviewQuestionInfo
            {
                Id = Guid.NewGuid(),
                Question = "Is this a different test question?",
                Rationale = "Different test rationale",
                ReviewId = Guid.NewGuid(),
                QuestionType = ReviewQuestionType.Requirement
            };

            // Act
            var result = question1.Equals(question2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Equals_NotReviewQuestionInfoType_ReturnsFalse()
        {
            // Arrange
            var question = new ReviewQuestionInfo
            {
                Id = Guid.NewGuid(),
                Question = "Is this a test question?",
                Rationale = "Test rationale",
                ReviewId = Guid.NewGuid(),
                QuestionType = ReviewQuestionType.Question
            };

            // Act
            var result = question.Equals(new object());

            // Assert
            Assert.False(result);
        }
    }
}