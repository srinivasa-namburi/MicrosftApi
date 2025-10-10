// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Flow.Contracts.Requests;

/// <summary>
/// Request to check the status of a streaming Flow query.
/// </summary>
public class FlowQueryStatusRequest
{
    /// <summary>
    /// Optional Flow conversation ID to check status for.
    /// If not provided, will check the current MCP session's active conversation.
    /// </summary>
    [Description("Optional Flow conversation ID (GUID) to check status for. If not provided, checks the current session's active conversation.")]
    public string? flowConversationId { get; set; }

    /// <summary>
    /// Optional subscription ID to verify stream subscription status.
    /// </summary>
    [Description("Optional subscription ID from the original stream query")]
    public string? subscriptionId { get; set; }
}