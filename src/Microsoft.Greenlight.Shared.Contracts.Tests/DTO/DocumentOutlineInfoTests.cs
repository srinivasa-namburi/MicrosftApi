namespace Microsoft.Greenlight.Shared.Contracts.DTO.Tests
{
    public class DocumentOutlineInfoTests
    {
        [Fact]
        public void Equals_SameProperties_ReturnsTrue()
        {
            // Arrange
            var outlineItems = new List<DocumentOutlineItemInfo>
            {
                new() {
                    Id = Guid.NewGuid(),
                    SectionNumber = "1",
                    SectionTitle = "Title 1",
                    Level = 0,
                    Children = []
                }
            };

            var documentOutlineInfo1 = new DocumentOutlineInfo
            {
                Id = Guid.NewGuid(),
                DocumentProcessDefinitionId = Guid.NewGuid(),
                OutlineItems = outlineItems
            };

            var documentOutlineInfo2 = new DocumentOutlineInfo
            {
                Id = documentOutlineInfo1.Id,
                DocumentProcessDefinitionId = documentOutlineInfo1.DocumentProcessDefinitionId,
                OutlineItems = outlineItems
            };

            // Act
            var result = documentOutlineInfo1.Equals(documentOutlineInfo2);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Equals_DifferentProperties_ReturnsFalse()
        {
            // Arrange
            var documentOutlineInfo1 = new DocumentOutlineInfo
            {
                Id = Guid.NewGuid(),
                DocumentProcessDefinitionId = Guid.NewGuid(),
                OutlineItems =
                [
                    new() {
                        Id = Guid.NewGuid(),
                        SectionNumber = "1",
                        SectionTitle = "Title 1",
                        Level = 0,
                        Children = []
                    }
                ]
            };

            var documentOutlineInfo2 = new DocumentOutlineInfo
            {
                Id = Guid.NewGuid(),
                DocumentProcessDefinitionId = Guid.NewGuid(),
                OutlineItems =
                [
                    new() {
                        Id = Guid.NewGuid(),
                        SectionNumber = "2",
                        SectionTitle = "Title 2",
                        Level = 0,
                        Children = []
                    }
                ]
            };

            // Act
            var result = documentOutlineInfo1.Equals(documentOutlineInfo2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Equals_NotDocumentOutlineInfoType_ReturnsFalse()
        {
            // Arrange
            var documentOutlineInfo = new DocumentOutlineInfo
            {
                Id = Guid.NewGuid(),
                DocumentProcessDefinitionId = Guid.NewGuid(),
                OutlineItems =
                [
                    new() {
                        Id = Guid.NewGuid(),
                        SectionNumber = "1",
                        SectionTitle = "Title 1",
                        Level = 0,
                        Children = []
                    }
                ]
            };

            // Act
            var result = documentOutlineInfo.Equals(new object());

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void FullText_WhenOnlyLevelZeroItems_ReturnsTextWithNoIndentations()
        {
            // Arrange
            var sectionNumber1 = "1";
            var sectionTitle1 = "Introduction";
            var sectionNumber2 = "2";
            var sectionTitle2 = "Main Content";

            var expectedText = $"{sectionNumber1} {sectionTitle1}\n{sectionNumber2} {sectionTitle2}\n";

            var documentOutlineInfo = new DocumentOutlineInfo
            {
                OutlineItems =
                [
                    new DocumentOutlineItemInfo
                    {
                        SectionNumber = sectionNumber1,
                        SectionTitle = sectionTitle1,
                        Level = 0,
                        Children = []
                    },
                    new DocumentOutlineItemInfo
                    {
                        SectionNumber = sectionNumber2,
                        SectionTitle = sectionTitle2,
                        Level = 0,
                        Children = []
                    }
                ]
            };

            // Act
            var fullText = documentOutlineInfo.FullText;

            // Assert
            Assert.Equal(expectedText, fullText);
        }

        [Fact]
        public void FullText_WhenChildrenItemsPresent_ReturnsTextWithIndentations()
        {
            // Arrange
            var sectionNumber1 = "1";
            var sectionTitle1 = "Introduction";
            var subsectionNumber = "1.1";
            var subsectionTitle = "Background";
            var sectionNumber2 = "2";
            var sectionTitle2 = "Main Content";

            var expectedText = $"{sectionNumber1} {sectionTitle1}\n  {subsectionNumber} {subsectionTitle}\n{sectionNumber2} {sectionTitle2}\n";

            var documentOutlineInfo = new DocumentOutlineInfo
            {
                OutlineItems =
            [
                new DocumentOutlineItemInfo
                {
                    SectionNumber = sectionNumber1,
                    SectionTitle = sectionTitle1,
                    Level = 0,
                    Children =
                    [
                        new DocumentOutlineItemInfo
                        {
                            SectionNumber = subsectionNumber,
                            SectionTitle = subsectionTitle,
                            Level = 1,
                            Children = []
                        }
                    ]
                },
                new DocumentOutlineItemInfo
                {
                    SectionNumber = sectionNumber2,
                    SectionTitle = sectionTitle2,
                    Level = 0,
                    Children = []
                }
            ]
            };

            // Act
            var fullText = documentOutlineInfo.FullText;

            // Assert
            Assert.Equal(expectedText, fullText);
        }

        [Fact]
        public void FullText_WhenNoLevelZeroItems_ReturnsEmptyText()
        {
            // Arrange
            var documentOutlineInfo = new DocumentOutlineInfo
            {
                OutlineItems = []
            };

            // Act
            var fullText = documentOutlineInfo.FullText;

            // Assert
            Assert.Equal(string.Empty, fullText);
        }
    }
}