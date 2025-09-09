// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Models.FileStorage;

/// <summary>
/// Join entity mapping a ContentReferenceItem to a FileAcknowledgmentRecord (per source file).
/// Mirrors the IngestedDocumentFileAcknowledgment pattern for content references.
/// </summary>
public class ContentReferenceFileAcknowledgment : EntityBase
{
    /// <summary>
    /// Content reference item ID this file acknowledgment applies to.
    /// </summary>
    public Guid ContentReferenceItemId { get; set; }

    /// <summary>
    /// Navigation property to the content reference item.
    /// </summary>
    public virtual ContentReferenceItem ContentReferenceItem { get; set; } = null!;

    /// <summary>
    /// Linked file acknowledgment record ID.
    /// </summary>
    public Guid FileAcknowledgmentRecordId { get; set; }

    /// <summary>
    /// Navigation property to the file acknowledgment record.
    /// </summary>
    public virtual FileAcknowledgmentRecord FileAcknowledgmentRecord { get; set; } = null!;
}

