// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Full generation status including per-node details for a document.
/// </summary>
public class DocumentGenerationFullStatusInfo
{
    /// <summary>
    /// Generated document ID.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Aggregated overall status across all nodes.
    /// </summary>
    public ContentNodeGenerationState Status { get; set; }

    /// <summary>
    /// Root content node statuses.
    /// </summary>
    public List<DocumentGenerationNodeStatusInfo> RootNodes { get; set; } = [];
}
