// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.Tests.DocumentProcess;

public class DocumentOutlineTrailingPeriodTest
{
    [Fact]
    public void FullText_NumbersWithTrailingPeriods_ParsesCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1. Introduction\n1.1. Project Overview\n1.2. Applicant Information\n2. Environmental Description";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal(2, documentOutline.OutlineItems.Count);
        Assert.Equal("Introduction", documentOutline.OutlineItems[0].SectionTitle);
        Assert.Equal("Environmental Description", documentOutline.OutlineItems[1].SectionTitle);
        Assert.Equal("1", documentOutline.OutlineItems[0].SectionNumber);
        Assert.Equal("2", documentOutline.OutlineItems[1].SectionNumber);
    }

    [Fact]
    public void FullText_NumbersWithoutTrailingPeriods_ParsesCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1 Introduction\n1.1 Project Overview\n1.2 Applicant Information\n2 Environmental Description";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal(2, documentOutline.OutlineItems.Count);
        Assert.Equal("Introduction", documentOutline.OutlineItems[0].SectionTitle);
        Assert.Equal("Environmental Description", documentOutline.OutlineItems[1].SectionTitle);
        Assert.Equal("1", documentOutline.OutlineItems[0].SectionNumber);
        Assert.Equal("2", documentOutline.OutlineItems[1].SectionNumber);
    }

    [Fact]
    public void FullText_MixedFormats_ParsesCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1. Introduction\n1.1 Project Overview\n1.2. Applicant Information\n2 Environmental Description";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal(2, documentOutline.OutlineItems.Count);
        Assert.Equal("Introduction", documentOutline.OutlineItems[0].SectionTitle);
        Assert.Equal("Environmental Description", documentOutline.OutlineItems[1].SectionTitle);
        Assert.Equal("1", documentOutline.OutlineItems[0].SectionNumber);
        Assert.Equal("2", documentOutline.OutlineItems[1].SectionNumber);
    }

    [Fact]
    public void FullText_TrailingPeriodsDoNotCreatePhantomLevels()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = "1. Introduction\n1.1. Project Overview\n1.1.1. Topography";

        // Act
        documentOutline.FullText = text;

        // Assert
        // Verify level calculation is correct (no phantom levels from trailing periods)
        Assert.Equal(0, documentOutline.OutlineItems[0].Level); // "1" should be level 0
        Assert.Equal(1, documentOutline.OutlineItems[0].Children[0].Level); // "1.1" should be level 1
        Assert.Equal(2, documentOutline.OutlineItems[0].Children[0].Children[0].Level); // "1.1.1" should be level 2
        
        // Verify section numbers don't include trailing periods
        Assert.Equal("1", documentOutline.OutlineItems[0].SectionNumber);
        Assert.Equal("1.1", documentOutline.OutlineItems[0].Children[0].SectionNumber);
        Assert.Equal("1.1.1", documentOutline.OutlineItems[0].Children[0].Children[0].SectionNumber);
    }

    [Fact]
    public void FullText_ComplexHierarchyWithTrailingPeriods_ParsesCorrectly()
    {
        // Arrange
        var documentOutline = new DocumentOutline();
        var text = @"1. Introduction
1.1. Project Overview
1.2. Applicant Information
1.3. Site Location
1.4. Regulatory Requirements
2. Environmental Description
2.1. Land Use and Geology
2.1.1. Topography
2.1.2. Soil Characteristics
2.1.3. Seismic Conditions
2.2. Water Resources
2.2.1. Surface Water
2.2.2. Groundwater";

        // Act
        documentOutline.FullText = text;

        // Assert
        Assert.Equal(2, documentOutline.OutlineItems.Count);
        
        // Check first section
        var section1 = documentOutline.OutlineItems[0];
        Assert.Equal("1", section1.SectionNumber);
        Assert.Equal("Introduction", section1.SectionTitle);
        Assert.Equal(0, section1.Level);
        Assert.Equal(4, section1.Children.Count);
        
        // Check second section
        var section2 = documentOutline.OutlineItems[1];
        Assert.Equal("2", section2.SectionNumber);
        Assert.Equal("Environmental Description", section2.SectionTitle);
        Assert.Equal(0, section2.Level);
        Assert.Equal(2, section2.Children.Count);
        
        // Check nested sections
        var section2_1 = section2.Children[0];
        Assert.Equal("2.1", section2_1.SectionNumber);
        Assert.Equal("Land Use and Geology", section2_1.SectionTitle);
        Assert.Equal(1, section2_1.Level);
        Assert.Equal(3, section2_1.Children.Count);
        
        // Check deeply nested section
        var section2_1_1 = section2_1.Children[0];
        Assert.Equal("2.1.1", section2_1_1.SectionNumber);
        Assert.Equal("Topography", section2_1_1.SectionTitle);
        Assert.Equal(2, section2_1_1.Level);
        Assert.Empty(section2_1_1.Children);
    }
}