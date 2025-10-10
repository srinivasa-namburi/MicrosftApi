// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel;

namespace Microsoft.Greenlight.McpServer.Core.Contracts.Requests;

/// <summary>
/// Request for getting document generation status.
/// </summary>
public sealed record GetDocumentStatusRequest
{
    /// <summary>
    /// Generated document ID (GUID as string). Use the ID returned by start_document_generation.
    /// </summary>
    [Description("Generated document ID (GUID string) returned by start_document_generation")]
    public required string documentId { get; init; }
}
