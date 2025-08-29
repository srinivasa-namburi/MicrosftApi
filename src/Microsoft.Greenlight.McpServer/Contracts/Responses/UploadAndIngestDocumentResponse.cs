// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.McpServer.Contracts.Responses;

/// <summary>
/// Response for upload and ingest document.
/// </summary>
public sealed class UploadAndIngestDocumentResponse
{
    /// <summary>
    /// The new ingested document ID.
    /// </summary>
    public required Guid IngestedDocumentId { get; init; }
}
