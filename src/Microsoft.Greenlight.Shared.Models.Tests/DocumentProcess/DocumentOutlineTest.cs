using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.Tests.DocumentProcess;

public class DocumentOutlineTest
{
    [Fact]
    public void FullText_EmptyText_ReturnEmptyOutlineItems()
    {
        // Arrange
        var documentOutline = new DocumentOutline
        {
            // Act
            FullText = string.Empty
        };

        // Assert
        Assert.Empty(documentOutline.OutlineItems);
    }

    [Fact]
    public void FullText_StringWithNumbers_ReturnRenderedTextCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1 Section One\n1.1 Subsection One";

        // Act
        documentOutline.FullText = text;
        var renderedText = documentOutline.FullText;

        // Assert
        var expected = "1 Section One\n  1.1 Subsection One\n";
        Assert.Equal(expected.Trim(), renderedText.Trim());
    }

    [Fact]
    public void FullText_StringWithNumbersOnMultipleLevels_ReturnRenderedTextCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1 Section One\n1.1 Subsection One\n1.1.1 Subsubsection One";

        // Act
        documentOutline.FullText = text;
        var renderedText = documentOutline.FullText;

        // Assert
        var expected = "1 Section One\n  1.1 Subsection One\n    1.1.1 Subsubsection One\n";
        Assert.Equal(expected, renderedText);
    }

    [Fact]
    public void FullText_StringWithNumbers_ReturnCorrectItemsCount()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1 Section One\n1.1 Subsection One\n1.2 Subsection Two\n2 Section Two";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal(2, documentOutline.OutlineItems.Count);
    }

    [Fact]
    public void FullText_StringWithNumbers_ReturnParsedSectionTitlesCorrectly()
    {
        //Arrange
        var documentOutline = new DocumentOutline();
        var text = "1 Section One\n1.1 Subsection One\n1.2 Subsection Two\n2 Section Two";
        //Act
        documentOutline.FullText = text;
        //Assert
        Assert.Equal("Section One", documentOutline.OutlineItems[0].SectionTitle);
        Assert.Equal("Section Two", documentOutline.OutlineItems[1].SectionTitle);
    }

    [Fact]
    public void FullText_StringWithNumbers_ReturnParsedSubsectionTitlesCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1 Section One\n1.1 Subsection One\n1.2 Subsection Two\n2 Section Two";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal("Subsection One", documentOutline.OutlineItems[0].Children[0].SectionTitle);
        Assert.Equal("Subsection Two", documentOutline.OutlineItems[0].Children[1].SectionTitle);
    }

    [Fact]
    public void FullText_StringWithNumbers_ReturnCorrectCountOfChildren()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1 Section One\n1.1 Subsection One\n1.2 Subsection Two\n2 Section Two";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal(2, documentOutline.OutlineItems[0].Children.Count);
    }

    [Fact]
    public void FullText_StringWithNumbers_ReturnParsedSectionNumbersCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1 Section One\n1.1 Subsection One\n1.2 Subsection Two\n2 Section Two";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal("1", documentOutline.OutlineItems[0].SectionNumber);
        Assert.Equal("2", documentOutline.OutlineItems[1].SectionNumber);
    }

    [Fact]
    public void FullText_StringWithNumbers_ReturnParsedSubsectionNumbersCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1 Section One\n1.1 Subsection One\n1.2 Subsection Two\n2 Section Two";
        var text2 = "1 Section One\n1.1 Subsection One\n1.1.1 Subsubsection One\n1.2 Subsection Two\n2 Section Two";

        // Act
        documentOutline.FullText = text;
        documentOutline.FullText = text2;

        // Assert
        Assert.Equal("1.1", documentOutline.OutlineItems[0].Children[0].SectionNumber);
        Assert.Equal("1.2", documentOutline.OutlineItems[0].Children[1].SectionNumber);
        Assert.Equal("1.1.1", documentOutline.OutlineItems[0].Children[0].Children[0].SectionNumber);
    }

    [Fact]
    public void FullText_StringWithNumbers_ReturnParsedSubsubsectionNumbersCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text= "1 Section One\n1.1 Subsection One\n1.1.1 Subsubsection One\n1.2 Subsection Two\n2 Section Two";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal("1.1.1", documentOutline.OutlineItems[0].Children[0].Children[0].SectionNumber);
    }

    
    [Fact]
    public void FullText_StringWithHash_ReturnCorrectCountOfChildren()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "# Section One\n## Subsection One\n## Subsection Two\n# Section Two";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal(2, documentOutline.OutlineItems[0].Children.Count);
    }
}
