using Microsoft.Greenlight.Shared.Models;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests
{
    public class ContentNodeExtensionsTests
    {
        [Fact]
        public void RemoveReservedWordsFromHeading_WhenTextStartsWithReservedWord_ShouldRemove()
        {
            // Arrange
            var contentNode = new ContentNode { Text = "Chapter 1: Introduction" };
            // Act
            contentNode.RemoveReservedWordsFromHeading();
            // Assert
            Assert.Equal("1: Introduction", contentNode.Text);
        }

        [Fact]
        public void RemoveReservedWordsFromHeading_WhenTextDoesNotStartWithReservedWord__ShouldNotRemove()
        {
            // Arrange
            var contentNode = new ContentNode { Text = "Introduction" };
            // Act
            contentNode.RemoveReservedWordsFromHeading();
            // Assert
            Assert.Equal("Introduction", contentNode.Text);
        }
    }
}
