// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Overall generation status for a generated document.
/// </summary>
public class DocumentGenerationStatusInfo
{
    /// <summary>
    /// Unique identifier of the generated document.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Aggregated generation status across all content nodes in the document.
    /// </summary>
    public ContentNodeGenerationState Status { get; set; }

    /// <summary>
    /// Optional summary count of nodes by status.
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Optional count of nodes currently in progress (includes outline-only).
    /// </summary>
    public int InProgressNodes { get; set; }

    /// <summary>
    /// Optional count of nodes completed successfully.
    /// </summary>
    public int CompletedNodes { get; set; }

    /// <summary>
    /// Optional count of nodes that failed during generation.
    /// </summary>
    public int FailedNodes { get; set; }
}
