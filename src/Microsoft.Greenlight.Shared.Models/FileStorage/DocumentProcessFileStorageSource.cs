// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Models.FileStorage;

/// <summary>
/// Represents the association between a document process and a file storage source.
/// </summary>
public class DocumentProcessFileStorageSource : EntityBase
{
    /// <summary>
    /// Unique identifier of the associated document process.
    /// </summary>
    public Guid DocumentProcessId { get; set; }

    /// <summary>
    /// Navigation property for the associated document process.
    /// </summary>
    public virtual DynamicDocumentProcessDefinition DocumentProcess { get; set; } = null!;

    /// <summary>
    /// Unique identifier of the associated file storage source.
    /// </summary>
    public Guid FileStorageSourceId { get; set; }

    /// <summary>
    /// Navigation property for the associated file storage source.
    /// </summary>
    public virtual FileStorageSource FileStorageSource { get; set; } = null!;

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