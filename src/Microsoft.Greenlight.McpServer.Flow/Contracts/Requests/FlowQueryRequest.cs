// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Flow.Contracts.Requests;

/// <summary>
/// Request model for Flow query operations.
/// </summary>
public record FlowQueryRequest
{
    /// <summary>
    /// The user's message/query.
    /// </summary>
    [Description("The user's message or query to process through Flow orchestration")]
    public string message { get; init; } = string.Empty;

    /// <summary>
    /// Optional Flow conversation ID for conversation continuity.
    /// When provided, this query will continue the specified Flow conversation.
    /// If not provided, a new Flow conversation will be created.
    /// </summary>
    [Description("Optional Flow conversation ID (GUID) for conversation continuity. When provided, continues the specified Flow conversation. If not provided, creates a new conversation.")]
    public string? flowConversationId { get; init; }

    /// <summary>
    /// Optional context or additional instructions for the query.
    /// </summary>
    [Description("Optional additional context or instructions to guide the Flow orchestration")]
    public string? context { get; init; }

    /// <summary>
    /// Indicates whether the caller can accept direct streaming push updates.
    /// Defaults to false to preserve existing MCP polling behaviour.
    /// </summary>
    [Description("Optional flag indicating the client can accept streaming push updates. Defaults to false.")]
    public bool acceptStreamingUpdates { get; init; }
}
