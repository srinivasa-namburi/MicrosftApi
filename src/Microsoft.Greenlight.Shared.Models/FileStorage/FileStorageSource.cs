// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Models.FileStorage;

/// <summary>
/// Represents a specific file storage source (container/folder/path) within a file storage host.
/// </summary>
public class FileStorageSource : EntityBase
{
    /// <summary>
    /// Display name of the file storage source.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Unique identifier of the file storage host this source belongs to.
    /// </summary>
    public Guid FileStorageHostId { get; set; }

    /// <summary>
    /// Navigation property for the file storage host.
    /// </summary>
    public virtual FileStorageHost FileStorageHost { get; set; } = null!;

    /// <summary>
    /// Container name, folder path, or equivalent organizational unit within the host.
    /// </summary>
    public required string ContainerOrPath { get; set; }

    /// <summary>
    /// Auto-import folder name within the container/path.
    /// Only relevant when ShouldMoveFiles is true.
    /// </summary>
    public string? AutoImportFolderName { get; set; }

    /// <summary>
    /// Indicates whether this is the default file storage source for its host.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Indicates whether the file storage source is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// For blob storage providers: indicates whether files should be moved (true) or just acknowledged (false).
    /// When false, AutoImportFolderName is not used.
    /// Default is false (acknowledge-only) for new sources, but legacy/default blob storage maintains move behavior.
    /// </summary>
    public bool ShouldMoveFiles { get; set; } = false;

    /// <summary>
    /// Description or notes about this storage source.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Primary/legacy data type for this storage source (for backward compatibility).
    /// Use <see cref="FileStorageSourceCategory"/> for multi-category support.
    /// </summary>
    public FileStorageSourceDataType StorageSourceDataType { get; set; } = FileStorageSourceDataType.Ingestion;

    /// <summary>
    /// Navigation property for document processes that use this storage source.
    /// </summary>
    public virtual ICollection<DocumentProcessFileStorageSource> DocumentProcessSources { get; set; } = [];

    /// <summary>
    /// Navigation property for document libraries that use this storage source.
    /// </summary>
    public virtual ICollection<DocumentLibraryFileStorageSource> DocumentLibrarySources { get; set; } = [];

    /// <summary>
    /// Optional categories this storage source participates in.
    /// </summary>
    public virtual ICollection<FileStorageSourceCategory> Categories { get; set; } = [];
}
