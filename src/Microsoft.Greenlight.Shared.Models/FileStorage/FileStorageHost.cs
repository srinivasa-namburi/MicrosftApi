// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.FileStorage;

/// <summary>
/// Represents a file storage host configuration (e.g., Azure Storage Account, local filesystem root, SharePoint site).
/// </summary>
public class FileStorageHost : EntityBase
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
    /// For BlobStorage: connection string or account name.
    /// For LocalFileSystem: root directory path.
    /// For SharePoint: site URL and credentials.
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

    /// <summary>
    /// Navigation property for file storage sources that use this host.
    /// </summary>
    public virtual ICollection<FileStorageSource> Sources { get; set; } = [];
}