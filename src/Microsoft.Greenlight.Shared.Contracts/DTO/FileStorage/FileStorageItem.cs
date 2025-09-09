// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;

/// <summary>
/// Represents a file item discovered by a file storage provider.
/// </summary>
public class FileStorageItem
{
    /// <summary>
    /// Relative path/name of the file within the storage source.
    /// </summary>
    public required string RelativeFilePath { get; set; }

    /// <summary>
    /// Full URL or path to access the file.
    /// </summary>
    public required string FullPath { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Last modified date of the file.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Content hash of the file (if available).
    /// </summary>
    public string? ContentHash { get; set; }

    /// <summary>
    /// MIME type of the file (if determinable).
    /// </summary>
    public string? MimeType { get; set; }
}