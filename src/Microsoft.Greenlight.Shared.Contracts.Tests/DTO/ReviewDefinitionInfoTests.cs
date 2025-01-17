namespace Microsoft.Greenlight.Shared.Contracts.DTO.Tests
{
    public class ReviewDefinitionInfoTests
    {
        [Fact]
        public void Equals_SameId_ReturnsTrue()
        {
            // Arrange
            var id = Guid.NewGuid();
            var review1 = new ReviewDefinitionInfo { Id = id, Title = "Review 1" };
            var review2 = new ReviewDefinitionInfo { Id = id, Title = "Review 2" };

            // Act
            var result = review1.Equals(review2);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Equals_DifferentId_ReturnsFalse()
        {
            // Arrange
            var review1 = new ReviewDefinitionInfo { Id = Guid.NewGuid(), Title = "Review 1" };
            var review2 = new ReviewDefinitionInfo { Id = Guid.NewGuid(), Title = "Review 2" };

            // Act
            var result = review1.Equals(review2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Equals_NotReviewDefinitionInfoType_ReturnsFalse()
        {
            // Arrange
            var review = new ReviewDefinitionInfo { Id = Guid.NewGuid(), Title = "Review 1" };

            // Act
            var result = review.Equals(new object());

            // Assert
            Assert.False(result);
        }
    }
}