// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Contracts.Requests;

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
    /// Optional MCP session ID for conversation continuity.
    /// If provided, continues existing Flow conversation.
    /// If not provided, creates new Flow conversation.
    /// </summary>
    [Description("Optional session ID for conversation continuity. If not provided, will be extracted from request headers or a new session created.")]
    public string? sessionId { get; init; }

    /// <summary>
    /// Optional context or additional instructions for the query.
    /// </summary>
    [Description("Optional additional context or instructions to guide the Flow orchestration")]
    public string? context { get; init; }
}