// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Flow.Contracts.Requests;

/// <summary>
/// Request for retrieving document process metadata fields.
/// </summary>
public sealed record GetMetadataFieldsRequest
{
    /// <summary>
    /// Document process ID (GUID as string).
    /// </summary>
    [Description("Document process ID (GUID as string)")]
    public string? processId { get; init; }

    /// <summary>
    /// Document process short name.
    /// </summary>
    [Description("Document process short name")]
    public string? processShortName { get; init; }
}
