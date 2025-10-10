// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.Greenlight.Grains.Chat.Contracts.Models;

/// <summary>
/// Represents the aggregated state for a Flow orchestration message while it is being composed.
/// </summary>
public class MessageAggregationState
{
    /// <summary>
    /// Gets or sets the identifier of the user-facing message.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the aggregation message emitted to the client, when available.
    /// </summary>
    public Guid? AggregationMessageId { get; set; }

    /// <summary>
    /// Gets or sets the aggregated sections keyed by backend document process name and their completion state.
    /// </summary>
    public Dictionary<string, (string Text, bool Complete)> AggregationSections { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets a value indicating whether the final synthesized response has been emitted.
    /// </summary>
    public bool FinalSynthesisEmitted { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last aggregation push to clients.
    /// </summary>
    public DateTime LastAggregationPushUtc { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the length of the last aggregation payload sent to the client.
    /// </summary>
    public int LastAggregationLength { get; set; }

    /// <summary>
    /// Gets or sets the backend status messages collected for this aggregation.
    /// </summary>
    public List<BackendStatusMessage> CollectedStatusMessages { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the last synthesized status message emission.
    /// </summary>
    public DateTime LastStatusSynthesis { get; set; } = DateTime.MinValue;
}

/// <summary>
/// Tracks the orchestration state for an MCP-driven flow task request.
/// </summary>
public class McpRequestState
{
    /// <summary>
    /// Gets or sets the identifier of the originating MCP request.
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user message associated with this request.
    /// </summary>
    public Guid UserMessageId { get; set; }

    /// <summary>
    /// Gets or sets the backend conversations that are still pending completion.
    /// </summary>
    public HashSet<Guid> PendingBackendConversations { get; set; } = new();

    /// <summary>
    /// Gets or sets the latest responses returned by each backend document process.
    /// </summary>
    public Dictionary<string, string> BackendResponses { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the final synthesized response when available.
    /// </summary>
    public string? FinalSynthesis { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this request was started.
    /// </summary>
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets a value indicating whether all backend conversations have completed.
    /// </summary>
    public bool AllBackendsComplete { get; set; }
}

/// <summary>
/// Represents a backend status message emitted during Flow orchestration.
/// </summary>
public class BackendStatusMessage
{
    /// <summary>
    /// Gets or sets the name of the document process that produced the status message.
    /// </summary>
    public string DocumentProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the textual status message.
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the status message was generated.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the backend considers the operation complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the status message should persist after completion.
    /// </summary>
    public bool IsPersistent { get; set; }
}
