// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.FileStorage;

/// <summary>
/// Many-to-many association between a ContentReferenceType and a FileStorageSource.
/// Allows multiple file storage sources to serve a given content reference type
/// and a file storage source to participate in multiple content reference types.
/// Mirrors the DP/DL association pattern for future flexibility.
/// </summary>
public class ContentReferenceTypeFileStorageSource : EntityBase
{
    /// <summary>
    /// The content reference type served by the associated storage source.
    /// </summary>
    public ContentReferenceType ContentReferenceType { get; set; }

    /// <summary>
    /// Unique identifier of the associated file storage source.
    /// </summary>
    public Guid FileStorageSourceId { get; set; }

    /// <summary>
    /// Navigation property for the associated file storage source.
    /// </summary>
    public virtual FileStorageSource FileStorageSource { get; set; } = null!;

    /// <summary>
    /// Priority order when multiple sources are defined (lower numbers are preferred).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Indicates whether this association is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates whether this storage source accepts uploads for this content reference type.
    /// </summary>
    public bool AcceptsUploads { get; set; } = false;
}

