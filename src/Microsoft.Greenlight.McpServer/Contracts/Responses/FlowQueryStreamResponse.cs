// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.McpServer.Contracts.Responses;

/// <summary>
/// Response for starting a streaming Flow query with Orleans streams integration.
/// </summary>
public class FlowQueryStreamResponse
{
    /// <summary>
    /// Session ID for the Flow conversation.
    /// </summary>
    public string? sessionId { get; set; }

    /// <summary>
    /// Unique subscription ID for tracking stream updates.
    /// </summary>
    public string? subscriptionId { get; set; }

    /// <summary>
    /// Processing task ID for tracking query progress.
    /// </summary>
    public string? processingTaskId { get; set; }

    /// <summary>
    /// Current status of the request ("processing", "error").
    /// </summary>
    public required string status { get; set; }

    /// <summary>
    /// Human-readable message about the query status.
    /// </summary>
    public string? message { get; set; }

    /// <summary>
    /// Error message if status is "error".
    /// </summary>
    public string? error { get; set; }

    /// <summary>
    /// Estimated time until completion (optional).
    /// </summary>
    public TimeSpan? estimatedCompletion { get; set; }
}