// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Backend.DocumentIngestion.Shared.Classification.Models;

public class DocumentClassificationResult
{
    public string ClassificationShortCode { get; set; }
    public DocumentClassificationType ClassificationType { get; set; }
    public float Confidence { get; set; }
    public bool SuccessfulClassification { get; set; } = false;
}
