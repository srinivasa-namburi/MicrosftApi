// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;

/// <summary>
/// Represents information about a file storage source.
/// </summary>
public class FileStorageSourceInfo
{
    /// <summary>
    /// Unique identifier of the file storage source.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the file storage source.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier of the file storage host this source belongs to.
    /// </summary>
    public Guid FileStorageHostId { get; set; }

    /// <summary>
    /// Information about the file storage host (populated when needed).
    /// This property is ignored during JSON serialization to prevent circular references.
    /// </summary>
    [JsonIgnore]
    public FileStorageHostInfo? FileStorageHost { get; set; }

    /// <summary>
    /// Container name, folder path, or equivalent organizational unit within the host.
    /// </summary>
    public string ContainerOrPath { get; set; } = string.Empty;

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
    /// Categorization for this storage source (Ingestion, ContentReference, MediaAssets).
    /// </summary>
    public FileStorageSourceDataType StorageSourceDataType { get; set; } = FileStorageSourceDataType.Ingestion;

    /// <summary>
    /// Optional list of categories this storage source participates in.
    /// </summary>
    public List<FileStorageSourceDataType> StorageSourceDataTypes { get; set; } = new();

    /// <summary>
    /// Date when the file storage source was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Date when the file storage source was last updated.
    /// </summary>
    public DateTime LastUpdatedDate { get; set; }

    // Legacy properties for backward compatibility with existing UI until all references are updated
    /// <summary>
    /// Type of the file storage provider (legacy - use FileStorageHost.ProviderType).
    /// </summary>
    [Obsolete("Use FileStorageHost.ProviderType instead")]
    public FileStorageProviderType ProviderType 
    { 
        get => FileStorageHost?.ProviderType ?? FileStorageProviderType.BlobStorage;
        set { /* Legacy setter - value is ignored, set via FileStorageHost */ }
    }

    /// <summary>
    /// Connection string (legacy - use FileStorageHost.ConnectionString).
    /// </summary>
    [Obsolete("Use FileStorageHost.ConnectionString instead")]
    public string ConnectionString 
    { 
        get => FileStorageHost?.ConnectionString ?? string.Empty;
        set { /* Legacy setter - value is ignored, set via FileStorageHost */ }
    }

    /// <summary>
    /// Authentication key (legacy - use FileStorageHost.AuthenticationKey).
    /// </summary>
    [Obsolete("Use FileStorageHost.AuthenticationKey instead")]
    public string? AuthenticationKey 
    { 
        get => FileStorageHost?.AuthenticationKey;
        set { /* Legacy setter - value is ignored, set via FileStorageHost */ }
    }
}
