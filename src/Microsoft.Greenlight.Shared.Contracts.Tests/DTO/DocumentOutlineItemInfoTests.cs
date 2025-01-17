namespace Microsoft.Greenlight.Shared.Contracts.DTO.Tests
{
    public class DocumentOutlineItemInfoTests
    {
        [Fact]
        public void Equals_SameProperties_ReturnsTrue()
        {
            // Arrange
            var documentOutlineItemInfo1 = new DocumentOutlineItemInfo
            {
                Id = Guid.NewGuid(),
                SectionNumber = "1",
                SectionTitle = "Title",
                PromptInstructions = "Instructions",
                RenderTitleOnly = true,
                Level = 1,
                ParentId = Guid.NewGuid(),
                DocumentOutlineId = Guid.NewGuid(),
                Children = [],
                OrderIndex = 0
            };

            var documentOutlineItemInfo2 = new DocumentOutlineItemInfo
            {
                Id = documentOutlineItemInfo1.Id,
                SectionNumber = documentOutlineItemInfo1.SectionNumber,
                SectionTitle = documentOutlineItemInfo1.SectionTitle,
                PromptInstructions = documentOutlineItemInfo1.PromptInstructions,
                RenderTitleOnly = documentOutlineItemInfo1.RenderTitleOnly,
                Level = documentOutlineItemInfo1.Level,
                ParentId = documentOutlineItemInfo1.ParentId,
                DocumentOutlineId = documentOutlineItemInfo1.DocumentOutlineId,
                Children = documentOutlineItemInfo1.Children,
                OrderIndex = documentOutlineItemInfo1.OrderIndex
            };

            // Act
            var result = documentOutlineItemInfo1.Equals(documentOutlineItemInfo2);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Equals_DifferentProperties_ReturnsFalse()
        {
            // Arrange
            var documentOutlineItemInfo1 = new DocumentOutlineItemInfo
            {
                Id = Guid.NewGuid(),
                SectionNumber = "1",
                SectionTitle = "Title",
                PromptInstructions = "Instructions",
                RenderTitleOnly = true,
                Level = 1,
                ParentId = Guid.NewGuid(),
                DocumentOutlineId = Guid.NewGuid(),
                Children = [],
                OrderIndex = 0
            };

            var documentOutlineItemInfo2 = new DocumentOutlineItemInfo
            {
                Id = Guid.NewGuid(),
                SectionNumber = "2",
                SectionTitle = "Different Title",
                PromptInstructions = "Different Instructions",
                RenderTitleOnly = false,
                Level = 2,
                ParentId = Guid.NewGuid(),
                DocumentOutlineId = Guid.NewGuid(),
                Children = [],
                OrderIndex = 1
            };

            // Act
            var result = documentOutlineItemInfo1.Equals(documentOutlineItemInfo2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Equals_NotDocumentOutlineItemInfoType_ReturnsFalse()
        {
            // Arrange
            var documentOutlineItemInfo = new DocumentOutlineItemInfo
            {
                Id = Guid.NewGuid(),
                SectionNumber = "1",
                SectionTitle = "Title",
                PromptInstructions = "Instructions",
                RenderTitleOnly = true,
                Level = 1,
                ParentId = Guid.NewGuid(),
                DocumentOutlineId = Guid.NewGuid(),
                Children = [],
                OrderIndex = 0
            };

            // Act
            var result = documentOutlineItemInfo.Equals(new object());

            // Assert
            Assert.False(result);
        }
    }
}