// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.FileStorage;

/// <summary>
/// Assigns one or more <see cref="FileStorageSourceDataType"/> categories to a <see cref="FileStorageSource"/>.
/// Enables a single source to participate in multiple functional areas.
/// </summary>
public class FileStorageSourceCategory : EntityBase
{
    /// <summary>
    /// Unique identifier of the associated file storage source.
    /// </summary>
    public Guid FileStorageSourceId { get; set; }

    /// <summary>
    /// Navigation property for the associated file storage source.
    /// </summary>
    public virtual FileStorageSource FileStorageSource { get; set; } = null!;

    /// <summary>
    /// The category of this storage source (Ingestion, ContentReference, MediaAssets).
    /// </summary>
    public FileStorageSourceDataType DataType { get; set; }
}

