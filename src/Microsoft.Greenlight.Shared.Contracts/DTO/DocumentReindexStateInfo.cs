// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents the state of a document reindexing operation.
/// </summary>
public class DocumentReindexStateInfo
{
    /// <summary>
    /// Unique identifier for this reindexing operation.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The short name of the document library or process being reindexed.
    /// </summary>
    public string DocumentLibraryShortName { get; set; } = string.Empty;

    /// <summary>
    /// The type of document library being reindexed.
    /// </summary>
    public DocumentLibraryType DocumentLibraryType { get; set; }

    /// <summary>
    /// The target container name for the reindexing operation.
    /// </summary>
    public string TargetContainerName { get; set; } = string.Empty;

    /// <summary>
    /// The current status of the reindexing operation.
    /// </summary>
    public ReindexOrchestrationState Status { get; set; }

    /// <summary>
    /// The reason for the reindexing operation.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Total number of documents to reindex.
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Number of documents that have been successfully reindexed.
    /// </summary>
    public int ProcessedDocuments { get; set; }

    /// <summary>
    /// Number of documents that failed to reindex.
    /// </summary>
    public int FailedDocuments { get; set; }

    /// <summary>
    /// List of errors encountered during reindexing.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Timestamp when the reindexing operation was last updated.
    /// </summary>
    public DateTime LastUpdatedUtc { get; set; }

    /// <summary>
    /// Timestamp when the reindexing operation started.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Timestamp when the reindexing operation completed.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}

/// <summary>
/// Response model for reindex start operations.
/// </summary>
public record ReindexStartResponse(string OrchestrationId, string DocumentLibrary, string Reason);