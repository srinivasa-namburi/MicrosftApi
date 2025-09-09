// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Models.FileStorage;

/// <summary>
/// Tracks acknowledgment of files from local file storage sources.
/// Used to maintain consistency without moving files.
/// </summary>
public class FileAcknowledgmentRecord : EntityBase
{
    /// <summary>
    /// Unique identifier of the associated file storage source.
    /// </summary>
    public Guid FileStorageSourceId { get; set; }

    /// <summary>
    /// Navigation property for the associated file storage source.
    /// </summary>
    public virtual FileStorageSource FileStorageSource { get; set; } = null!;

    /// <summary>
    /// Relative path of the acknowledged file within the storage source.
    /// </summary>
    public required string RelativeFilePath { get; set; }

    /// <summary>
    /// Internal storage URL or path that is typically authenticated and not accessible to external users.
    /// For blob storage, this would be the authenticated blob URL. For local storage, the full file path.
    /// </summary>
    public required string FileStorageSourceInternalUrl { get; set; }

    /// <summary>
    /// Hash of the file content at the time of acknowledgment.
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// Date when the file was acknowledged.
    /// </summary>
    public DateTime AcknowledgedDate { get; set; }

    /// <summary>
    /// Many-to-many mapping to ingested documents that have processed this file/version.
    /// </summary>
    public virtual List<IngestedDocumentFileAcknowledgment> IngestedDocumentLinks { get; set; } = [];
}