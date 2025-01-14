// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Greenlight.Shared.Models.Classification;

/// <summary>
/// Represents the result of a document classification process.
/// </summary>
public class DocumentClassificationResult
{
    /// <summary>
    /// Short code representing the result of the document classification.
    /// </summary>
    public string ClassificationShortCode { get; set; }

    /// <summary>
    /// Confidence level of the document classification process.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Value indicating whether the document classification was successful.
    /// </summary>
    public bool SuccessfulClassification { get; set; } = false;
}
