// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Models.FileStorage;

/// <summary>
/// Join entity mapping an IngestedDocument (per DP/DL instance) to a FileAcknowledgmentRecord (per source file).
/// Enables tracking which DP/DL ingested a given source file/version.
/// </summary>
public class IngestedDocumentFileAcknowledgment : EntityBase
{
    /// <summary>
    /// The ingested document id (represents per DP/DL ingestion instance).
    /// </summary>
    public Guid IngestedDocumentId { get; set; }

    /// <summary>
    /// Navigation property to the ingested document.
    /// </summary>
    public virtual IngestedDocument IngestedDocument { get; set; } = null!;

    /// <summary>
    /// The acknowledgment record id (represents the source file "seen" state).
    /// </summary>
    public Guid FileAcknowledgmentRecordId { get; set; }

    /// <summary>
    /// Navigation property to the acknowledgment record.
    /// </summary>
    public virtual FileAcknowledgmentRecord FileAcknowledgmentRecord { get; set; } = null!;
}
