// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Backend.DocumentIngestion.Shared.Classification.Models;

public enum DocumentClassificationType
{
    NrcHeadingsOnly = 100,
    NrcWithHeldSectionCover = 200,
    NrcEnvironmentalReportWithNumberedChapters = 300,
    NrcEnvironmentalReportWithMixedTitles = 400,
    Letter = 500,
    NrcFiguresAndTablesOnly = 600,
    CustomDataBasicDocument = 1100,
    CustomDataProductSpecificInput = 1200
}
