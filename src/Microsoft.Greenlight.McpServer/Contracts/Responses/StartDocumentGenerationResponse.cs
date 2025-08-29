// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.McpServer.Contracts.Responses;

/// <summary>
/// Response for starting document generation.
/// </summary>
public sealed class StartDocumentGenerationResponse
{
    /// <summary>
    /// Operation status (e.g., "started" or "failed").
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// The ID for the generation request (Guid).
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Optional error message if Status == "failed".
    /// </summary>
    public string? Error { get; init; }
}
