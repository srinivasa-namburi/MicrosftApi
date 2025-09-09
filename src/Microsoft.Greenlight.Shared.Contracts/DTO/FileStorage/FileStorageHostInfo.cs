// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;

/// <summary>
/// Represents information about a file storage host.
/// </summary>
public class FileStorageHostInfo
{
    /// <summary>
    /// Unique identifier of the file storage host.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the file storage host.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
    public string ConnectionString { get; set; } = string.Empty;

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
    /// Date when the file storage host was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Date when the file storage host was last updated.
    /// </summary>
    public DateTime LastUpdatedDate { get; set; }

    /// <summary>
    /// List of file storage sources associated with this host.
    /// </summary>
    public List<FileStorageSourceInfo> Sources { get; set; } = [];
}