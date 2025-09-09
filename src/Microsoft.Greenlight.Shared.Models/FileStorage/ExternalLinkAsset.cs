// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Models.FileStorage;

/// <summary>
/// Canonical storage-aware link for files used by Content References.
/// Created by storage services or on-demand by FileUrlResolverService and
/// consumed via FileController proxied routes.
/// </summary>
public class ExternalLinkAsset : EntityBase
{
    /// <summary>
    /// The URL where the file can be accessed.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// MIME type of the linked file.
    /// </summary>
    public required string MimeType { get; set; }

    /// <summary>
    /// Optional hash of the file content for deduplication and integrity checking.
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// Original file name provided by the user.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Size of the file in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Optional description or metadata about the file.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional reference to the file storage source that was used to store this file.
    /// When set, enables proper routing of download requests to the correct storage provider.
    /// </summary>
    public Guid? FileStorageSourceId { get; set; }

    /// <summary>
    /// Navigation property for the file storage source (when FileStorageSourceId is set).
    /// </summary>
    public virtual FileStorageSource? FileStorageSource { get; set; }
}
