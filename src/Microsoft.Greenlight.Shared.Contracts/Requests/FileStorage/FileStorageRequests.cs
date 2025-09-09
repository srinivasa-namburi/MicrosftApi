// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Requests.FileStorage;

/// <summary>
/// Request for creating a new file storage host.
/// </summary>
public class CreateFileStorageHostRequest
{
    /// <summary>
    /// Display name of the file storage host.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Type of the file storage provider.
    /// </summary>
    public FileStorageProviderType ProviderType { get; set; }

    /// <summary>
    /// Connection string or configuration data for the provider.
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Indicates whether this is the default/primary file storage host.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Indicates whether the file storage host is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional authentication key or token (encrypted/secured).
    /// </summary>
    public string? AuthenticationKey { get; set; }

    /// <summary>
    /// Description or notes about this storage host.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Request for updating an existing file storage host.
/// </summary>
public class UpdateFileStorageHostRequest
{
    /// <summary>
    /// Unique identifier of the file storage host to update.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the file storage host.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Type of the file storage provider.
    /// </summary>
    public FileStorageProviderType ProviderType { get; set; }

    /// <summary>
    /// Connection string or configuration data for the provider.
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Indicates whether this is the default/primary file storage host.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Indicates whether the file storage host is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional authentication key or token (encrypted/secured).
    /// </summary>
    public string? AuthenticationKey { get; set; }

    /// <summary>
    /// Description or notes about this storage host.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Request for creating a new file storage source.
/// </summary>
public class CreateFileStorageSourceRequest
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
    /// </summary>
    public bool ShouldMoveFiles { get; set; } = false;

    /// <summary>
    /// Description or notes about this storage source.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional categories this storage source participates in (Ingestion, ContentReference, MediaAssets).
    /// </summary>
    public List<FileStorageSourceDataType> StorageSourceDataTypes { get; set; } = new();
}

/// <summary>
/// Request for updating an existing file storage source.
/// </summary>
public class UpdateFileStorageSourceRequest
{
    /// <summary>
    /// Unique identifier of the file storage source to update.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the file storage source.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Unique identifier of the file storage host this source belongs to.
    /// </summary>
    public Guid FileStorageHostId { get; set; }

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
    /// </summary>
    public bool ShouldMoveFiles { get; set; } = false;

    /// <summary>
    /// Description or notes about this storage source.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional categories this storage source participates in (Ingestion, ContentReference, MediaAssets).
    /// </summary>
    public List<FileStorageSourceDataType> StorageSourceDataTypes { get; set; } = new();
}

/// <summary>
/// Request for updating a document process file storage source association.
/// </summary>
public class UpdateProcessSourceAssociationRequest
{
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
}

/// <summary>
/// Request for updating a document library file storage source association.
/// </summary>
public class UpdateLibrarySourceAssociationRequest
{
    /// <summary>
    /// Priority order for processing files from this source (lower numbers processed first).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Indicates whether this storage source is currently active for this document library.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates whether this storage source accepts file uploads from the user interface for this document library.
    /// Only one source per document library should have this set to true.
    /// </summary>
    public bool AcceptsUploads { get; set; } = false;
}
