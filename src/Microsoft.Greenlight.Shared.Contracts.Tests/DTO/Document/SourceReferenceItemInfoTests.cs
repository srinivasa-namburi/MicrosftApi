
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;

namespace Microsoft.Greenlight.Shared.Contracts.Tests.DTO.Document
{
    public class SourceReferenceItemInfoTests
    {
        [Fact]
        public void HasSourceReferenceLink_WhenSourceReferenceLinkIsNotEmpty_ShouldReturnTrue()
        {
            // Arrange
            var sourceReferenceItem = new TestSourceReferenceItemInfo
            {
                SourceReferenceLink = "http://test.com"
            };

            // Act
            var result = sourceReferenceItem.HasSourceReferenceLink;

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasSourceReferenceLink_WhenSourceReferenceLinkIsNull_ShouldReturnFalse()
        {
            // Arrange
            var sourceReferenceItem = new TestSourceReferenceItemInfo
            {
                SourceReferenceLink = null
            };

            // Act
            var result = sourceReferenceItem.HasSourceReferenceLink;

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasSourceReferenceLink_WhenSourceReferenceLinkIsEmpty_ShouldReturnFalse()
        {
            // Arrange
            var sourceReferenceItem = new TestSourceReferenceItemInfo
            {
                SourceReferenceLink = string.Empty
            };

            // Act
            var result = sourceReferenceItem.HasSourceReferenceLink;

            // Assert
            Assert.False(result);
        }

        private class TestSourceReferenceItemInfo : SourceReferenceItemInfo
        {
            // This class is used to test the abstract SourceReferenceItemInfo class
        }
    }
}