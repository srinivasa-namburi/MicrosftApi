// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Flow.Contracts.Requests;

/// <summary>
/// Request for starting a document generation orchestration.
/// </summary>
public sealed record StartDocumentGenerationRequest
{
    /// <summary>
    /// Process short name identifying the target document process.
    /// </summary>
    [Description("Process short name. Use list_document_processes to get valid short names")]
    public required string documentProcessName { get; init; }

    /// <summary>
    /// Title for the generated document.
    /// </summary>
    [Description("Title for the document")]
    public required string documentTitle { get; init; }

    /// <summary>
    /// Optional JSON payload for generation request. Keys should match metadata field names.
    /// </summary>
    [Description("Metadata fields (JSON) retrieved by using get_document_process_metadata_fields")]
    public string? metadataFields { get; init; }
}
