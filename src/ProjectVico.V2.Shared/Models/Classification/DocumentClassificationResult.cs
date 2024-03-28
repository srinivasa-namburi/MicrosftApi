// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.V2.Shared.Models.Classification;

public class DocumentClassificationResult
{
    public string ClassificationShortCode { get; set; }
    public float Confidence { get; set; }
    public bool SuccessfulClassification { get; set; } = false;
}
