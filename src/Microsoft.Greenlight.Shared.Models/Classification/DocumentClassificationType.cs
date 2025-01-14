// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Greenlight.Shared.Models.Classification;

/// <summary>
/// Represents different types of document classifications.
/// </summary>
public enum DocumentClassificationType
{
    /// <summary>
    /// Ingested document type has NRC Headings Only.
    /// </summary>
    NrcHeadingsOnly = 100,

    /// <summary>
    /// Ingested document type has NRC Withheld Section Cover.
    /// </summary>
    NrcWithHeldSectionCover = 200,

    /// <summary>
    /// Ingested document type is NRC Environmental Report With Numbered Chapters.
    /// </summary>
    NrcEnvironmentalReportWithNumberedChapters = 300,

    /// <summary>
    /// Ingested document type is NRC Environmental Report With Mixed Titles.
    /// </summary>
    NrcEnvironmentalReportWithMixedTitles = 400,

    /// <summary>
    /// Ingested document type is a letter.
    /// </summary>
    Letter = 500,

    /// <summary>
    /// Ingested document type has NRC Figures And Tables Only.
    /// </summary>
    NrcFiguresAndTablesOnly = 600,

    /// <summary>
    /// Ingested document type is a Custom Data Basic Document.
    /// </summary>
    CustomDataBasicDocument = 1100,

    /// <summary>
    /// Ingested document type is Custom Data Product Specific Input.
    /// </summary>
    CustomDataProductSpecificInput = 1200,

    /// <summary>
    /// Ingested document type is an Unknown Classification.
    /// </summary>
    UnknownClassification = 9999
}
