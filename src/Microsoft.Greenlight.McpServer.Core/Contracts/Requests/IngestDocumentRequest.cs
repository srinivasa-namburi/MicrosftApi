// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Core.Contracts.Requests;

/// <summary>
/// Request for registering and ingesting a document by URL.
/// </summary>
public sealed record IngestDocumentRequest
{
    /// <summary>
    /// Short name of the target document library or process.
    /// </summary>
    [Description("Short name of the document library or process")]
    public required string targetShortName { get; init; }

    /// <summary>
    /// Target type: "primary_process" or "additional_library".
    /// </summary>
    [Description("Type: primary_process or additional_library")]
    public required string targetType { get; init; }

    /// <summary>
    /// Original document URL (blob or http) from which content can be fetched.
    /// </summary>
    [Description("Original document URL (blob or http)")]
    public required string documentUrl { get; init; }

    /// <summary>
    /// File name including extension to associate with the ingested document.
    /// </summary>
    [Description("File name including extension")]
    public required string fileName { get; init; }

    /// <summary>
    /// Optional uploader user OID.
    /// </summary>
    [Description("Optional uploader user OID")]
    public string? uploaderOid { get; init; }
}
