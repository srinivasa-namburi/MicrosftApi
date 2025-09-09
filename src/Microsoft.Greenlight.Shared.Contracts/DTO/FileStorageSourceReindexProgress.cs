// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents the reindexing progress for a specific FileStorageSource.
/// </summary>
public class FileStorageSourceReindexProgress
{
    /// <summary>
    /// Unique identifier of the FileStorageSource.
    /// </summary>
    public Guid FileStorageSourceId { get; set; }

    /// <summary>
    /// Display name of the FileStorageSource.
    /// </summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>
    /// Type of the file storage provider (e.g., BlobStorage, LocalFileSystem, SharePoint).
    /// </summary>
    public FileStorageProviderType ProviderType { get; set; }

    /// <summary>
    /// Container or path for this source.
    /// </summary>
    public string ContainerOrPath { get; set; } = string.Empty;

    /// <summary>
    /// Total number of documents from this source to be reindexed.
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Number of documents from this source that have been successfully reindexed.
    /// </summary>
    public int ProcessedDocuments { get; set; }

    /// <summary>
    /// Number of documents from this source that failed to reindex.
    /// </summary>
    public int FailedDocuments { get; set; }

    /// <summary>
    /// List of errors specific to this source during reindexing.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Timestamp when this source's processing was last updated.
    /// </summary>
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Progress percentage for this source (0-100).
    /// </summary>
    public double ProgressPercentage => TotalDocuments > 0 ? (double)(ProcessedDocuments + FailedDocuments) / TotalDocuments * 100 : 0;

    /// <summary>
    /// Indicates if all documents from this source have been processed (successfully or failed).
    /// </summary>
    public bool IsCompleted => TotalDocuments > 0 && (ProcessedDocuments + FailedDocuments) >= TotalDocuments;
}