// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Contracts.Requests;

/// <summary>
/// Request to check the status of a streaming Flow query.
/// </summary>
public class FlowQueryStatusRequest
{
    /// <summary>
    /// Optional session ID of the Flow conversation to check.
    /// If not provided, will be extracted from request headers.
    /// </summary>
    [Description("Optional session ID. If not provided, will be extracted from request headers or current session.")]
    public string? sessionId { get; set; }

    /// <summary>
    /// Optional subscription ID to verify stream subscription status.
    /// </summary>
    [Description("Optional subscription ID from the original stream query")]
    public string? subscriptionId { get; set; }
}