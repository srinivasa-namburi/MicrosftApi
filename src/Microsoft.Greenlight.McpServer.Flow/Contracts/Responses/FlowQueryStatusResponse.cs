// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.McpServer.Flow.Contracts.Responses;

/// <summary>
/// Response containing the current status of a streaming Flow query.
/// </summary>
public class FlowQueryStatusResponse
{
    /// <summary>
    /// Flow conversation ID for tracking this conversation.
    /// </summary>
    public string? flowConversationId { get; set; }

    /// <summary>
    /// Current status ("created", "processing", "completed", "error", "cancelled").
    /// </summary>
    public required string status { get; set; }

    /// <summary>
    /// Processing progress as a percentage (0-100).
    /// </summary>
    public int progress { get; set; }

    /// <summary>
    /// List of document processes engaged for this query.
    /// </summary>
    public List<string> engagedProcesses { get; set; } = new();

    /// <summary>
    /// List of active backend conversation IDs.
    /// </summary>
    public List<string> activeConversationIds { get; set; } = new();

    /// <summary>
    /// Current synthesized response (may be partial if still processing).
    /// For completed queries, this contains the final aggregated response.
    /// </summary>
    public string? currentResponse { get; set; }

    /// <summary>
    /// Intermediate/in-progress response that was being built during processing.
    /// This shows the "working" message before final aggregation.
    /// </summary>
    public string? intermediateResponse { get; set; }

    /// <summary>
    /// Whether the query processing is complete.
    /// </summary>
    public bool isComplete { get; set; }

    /// <summary>
    /// Whether the stream subscription is still active.
    /// </summary>
    public bool subscriptionActive { get; set; }

    /// <summary>
    /// When the stream subscription expires.
    /// </summary>
    public DateTime? subscriptionExpiresAt { get; set; }

    /// <summary>
    /// Last activity timestamp for the Flow session.
    /// </summary>
    public DateTime lastActivityUtc { get; set; }

    /// <summary>
    /// Error message if status is "error".
    /// </summary>
    public string? error { get; set; }

    /// <summary>
    /// Additional status details or messages.
    /// </summary>
    public string? message { get; set; }
}