// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Flow.Contracts.Requests;

/// <summary>
/// Request to cancel a streaming Flow query and clean up resources.
/// </summary>
public class FlowQueryCancelRequest
{
    /// <summary>
    /// Optional Flow conversation ID to cancel.
    /// If not provided, cancellation will apply to the current MCP session's active conversation.
    /// </summary>
    [Description("Optional Flow conversation ID (GUID) to cancel. If not provided, cancels the current session's active conversation.")]
    public string? flowConversationId { get; set; }

    /// <summary>
    /// Optional subscription ID to ensure we're cancelling the right query.
    /// </summary>
    [Description("Optional subscription ID from the original stream query")]
    public string? subscriptionId { get; set; }

    /// <summary>
    /// Optional reason for cancellation.
    /// </summary>
    [Description("Optional reason for cancelling the query")]
    public string? reason { get; set; }
}