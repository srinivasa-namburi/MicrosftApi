// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Node-level generation status with recursive children; used for full status trees.
/// </summary>
public class DocumentGenerationNodeStatusInfo
{
    /// <summary>
    /// Content node ID.
    /// </summary>
    public Guid NodeId { get; set; }

    /// <summary>
    /// Node type.
    /// </summary>
    public ContentNodeType Type { get; set; }

    /// <summary>
    /// If true, the node renders only its title and shouldn't be counted complete based solely on itself.
    /// </summary>
    public bool RenderTitleOnly { get; set; }

    /// <summary>
    /// The node's own generation state (as stored on the node).
    /// </summary>
    public ContentNodeGenerationState? NodeState { get; set; }

    /// <summary>
    /// Status aggregated from this node and all children (bubbled up).
    /// </summary>
    public ContentNodeGenerationState AggregatedStatus { get; set; }

    /// <summary>
    /// Child node statuses.
    /// </summary>
    public List<DocumentGenerationNodeStatusInfo> Children { get; set; } = [];
}
