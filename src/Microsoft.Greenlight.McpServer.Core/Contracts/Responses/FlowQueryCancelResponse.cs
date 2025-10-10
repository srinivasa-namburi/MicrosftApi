// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.McpServer.Core.Contracts.Responses;

/// <summary>
/// Response for cancelling a streaming Flow query.
/// </summary>
public class FlowQueryCancelResponse
{
    /// <summary>
    /// Flow conversation ID of the cancelled conversation.
    /// </summary>
    public string? flowConversationId { get; set; }

    /// <summary>
    /// Status of the cancellation request ("cancelled", "error", "already_completed").
    /// </summary>
    public required string status { get; set; }

    /// <summary>
    /// Human-readable message about the cancellation.
    /// </summary>
    public string? message { get; set; }

    /// <summary>
    /// Error message if status is "error".
    /// </summary>
    public string? error { get; set; }

    /// <summary>
    /// List of backend conversations that were terminated.
    /// </summary>
    public List<string> terminatedConversations { get; set; } = new();
}