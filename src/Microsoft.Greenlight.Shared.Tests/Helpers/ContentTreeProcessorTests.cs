using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Moq;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Greenlight.Shared.Tests.Helpers
{
    public class ContentTreeProcessorTests
    {
        private readonly ContentTreeProcessor _contentTreeProcessor;

        public ContentTreeProcessorTests()
        {
            var mockOptions = new Mock<IOptions<ServiceConfigurationOptions>>();
            var mockOpenAiClient = new Mock<AzureOpenAIClient>();
            _contentTreeProcessor = new ContentTreeProcessor(mockOptions.Object, mockOpenAiClient.Object);
        }


        [Fact]
        public void FindSectionHeadings_WhenChildNodeContainsHeading_ShouldAddToSectionHeadingList()
        {
            // Arrange
            var sectionHeadingList = new List<ContentNode>();
            var nestedNodeBody = new ContentNode { Text = "Nested Heading", Type = ContentNodeType.BodyText, Children = [] };
            var nestedNodeHeader = new ContentNode { Text = "Nested Heading", Type = ContentNodeType.Heading, Children = [nestedNodeBody] };
            var childNode1 = new ContentNode { Text = "Sub Heading", Type = ContentNodeType.Heading, Children = [nestedNodeHeader] };
            var childNode2 = new ContentNode { Text = "Sub Heading", Type = ContentNodeType.Heading, Children = [] };
            var parentNode = new ContentNode { Text = "Root", Type = ContentNodeType.Heading, Children = [childNode1, childNode2] };

            // Act
            _contentTreeProcessor.FindSectionHeadings(parentNode, sectionHeadingList);

            // Assert
            Assert.Equal(3, sectionHeadingList.Count);
            Assert.Contains(childNode1, sectionHeadingList);
            Assert.Contains(childNode2, sectionHeadingList);
            Assert.Contains(nestedNodeHeader, sectionHeadingList);
        }

        [Fact]
        public void CountContentNodes_WhenNodeContainsNestedNodes_ShouldReturnAccurateCount()
        {
            // Arrange
            var nestedNodeBody = new ContentNode { Text = "Nested Heading", Type = ContentNodeType.BodyText, Children = [] };
            var nestedNodeHeader = new ContentNode { Text = "Nested Heading", Type = ContentNodeType.Heading, Children = [nestedNodeBody] };
            var childNode1 = new ContentNode { Text = "Sub Heading", Type = ContentNodeType.Heading, Children = [nestedNodeHeader] };
            var childNode2 = new ContentNode { Text = "Sub Heading", Type = ContentNodeType.Heading, Children = [] };
            var parentNode = new ContentNode { Text = "Root", Type = ContentNodeType.Heading, Children = [childNode1, childNode2] };

            // Act
            var result = _contentTreeProcessor.CountContentNodes(parentNode);

            // Assert
            Assert.Equal(5, result);
        }

        [Fact]
        public void FindLastTitleOrHeading_WhenNodeListIsEmpty_ShouldReturnNull()
        {
            // Arrange
            var contentNodeList = new List<ContentNode>();

            // Act
            var result = _contentTreeProcessor.FindLastTitleOrHeading(contentNodeList);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FindLastTitleOrHeading_WhenNodeListIsNotEmpty_ShouldReturnLastTitleOrHeadingNode()
        {
            // Arrange
            var nestedNodeBody = new ContentNode { Text = "Nested Heading", Type = ContentNodeType.BodyText, Children = [] };
            var nestedNodeHeader = new ContentNode { Text = "Nested Heading", Type = ContentNodeType.Heading, Children = [nestedNodeBody] };
            var childNode1 = new ContentNode { Text = "Sub Heading", Type = ContentNodeType.Heading, Children = [] };
            var childNode2 = new ContentNode { Text = "Sub Heading", Type = ContentNodeType.Heading, Children = [nestedNodeHeader] };
            var parentNode = new ContentNode { Text = "Root", Type = ContentNodeType.Heading, Children = [childNode1, childNode2] };
            var contentNodeList = new List<ContentNode> { parentNode };

            // Act
            var result = _contentTreeProcessor.FindLastTitleOrHeading(contentNodeList);

            // Assert
            Assert.Equal(result, nestedNodeHeader);
        }
    }
}
