// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts.State;

/// <summary>
/// Represents the state of a document reindexing operation.
/// </summary>
[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public class DocumentReindexState
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
    public ReindexOrchestrationState Status { get; set; } = ReindexOrchestrationState.NotStarted;

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
    public List<string> Errors { get; set; } = new List<string>();

    /// <summary>
    /// Timestamp when the reindexing operation was last updated.
    /// </summary>
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the reindexing operation started.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Timestamp when the reindexing operation completed.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}