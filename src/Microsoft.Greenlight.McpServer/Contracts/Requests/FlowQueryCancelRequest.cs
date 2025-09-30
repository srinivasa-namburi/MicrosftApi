// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Contracts.Requests;

/// <summary>
/// Request to cancel a streaming Flow query and clean up resources.
/// </summary>
public class FlowQueryCancelRequest
{
    /// <summary>
    /// Optional session ID of the Flow conversation to cancel.
    /// If not provided, will be extracted from request headers.
    /// </summary>
    [Description("Optional session ID. If not provided, will be extracted from request headers or current session.")]
    public string? sessionId { get; set; }

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