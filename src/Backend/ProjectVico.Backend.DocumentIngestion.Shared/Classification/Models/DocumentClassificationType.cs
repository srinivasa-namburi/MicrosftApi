// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Backend.DocumentIngestion.Shared.Classification.Models;

public enum DocumentClassificationType
{
    HeadingsOnly = 100,
    WithHeldSectionCover = 200,
    EnvironmentalReportWithNumberedChapters = 300,
    EnvironmentalReportWithMixedTitles = 400,
    Letter = 500,
    FiguresAndTablesOnly = 600

}
