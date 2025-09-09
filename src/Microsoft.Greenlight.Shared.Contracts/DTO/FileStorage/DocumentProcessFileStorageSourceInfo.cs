// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;

/// <summary>
/// Represents information about a document process file storage source association.
/// </summary>
public class DocumentProcessFileStorageSourceInfo
{
    /// <summary>
    /// Unique identifier of the association.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier of the associated document process.
    /// </summary>
    public Guid DocumentProcessId { get; set; }

    /// <summary>
    /// Name of the associated document process.
    /// </summary>
    public string DocumentProcessName { get; set; } = string.Empty;

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
    /// Priority order for processing files from this source (lower numbers processed first).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Indicates whether this storage source is currently active for this document process.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates whether this storage source accepts file uploads from the user interface for this document process.
    /// Only one source per document process should have this set to true.
    /// </summary>
    public bool AcceptsUploads { get; set; } = false;

    /// <summary>
    /// Date when the association was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Date when the association was last updated.
    /// </summary>
    public DateTime LastUpdatedDate { get; set; }
}