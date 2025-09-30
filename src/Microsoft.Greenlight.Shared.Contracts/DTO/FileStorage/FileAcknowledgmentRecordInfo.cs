// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;

/// <summary>
/// Represents information about a file acknowledgment record.
/// </summary>
public class FileAcknowledgmentRecordInfo
{
    /// <summary>
    /// Unique identifier of the acknowledgment record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier of the associated file storage source.
    /// </summary>
    public Guid FileStorageSourceId { get; set; }

    /// <summary>
    /// Name of the associated file storage source.
    /// </summary>
    public string FileStorageSourceName { get; set; } = string.Empty;

    /// <summary>
    /// Type of the file storage provider.
    /// </summary>
    public FileStorageProviderType ProviderType { get; set; }

    /// <summary>
    /// Relative path of the acknowledged file.
    /// </summary>
    public string RelativeFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Internal storage URL or path that is typically authenticated and not accessible to external users.
    /// For blob storage, this would be the authenticated blob URL. For local storage, the full file path.
    /// </summary>
    public string FileStorageSourceInternalUrl { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier of the associated ingested document (if any).
    /// </summary>
    public Guid? IngestedDocumentId { get; set; }

    /// <summary>
    /// Date when the file was acknowledged.
    /// </summary>
    public DateTime AcknowledgedDate { get; set; }

    /// <summary>
    /// UI-friendly display filename that should be shown to users.
    /// This provides the clean, original filename without path or GUID-based naming.
    /// </summary>
    public string? DisplayFileName { get; set; }
}