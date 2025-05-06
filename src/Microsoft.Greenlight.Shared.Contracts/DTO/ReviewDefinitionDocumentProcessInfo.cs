using System;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents the association between a review definition and a document process.
/// </summary>
public class ReviewDefinitionDocumentProcessInfo
{
    /// <summary>
    /// Unique identifier of the association.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique identifier of the review definition.
    /// </summary>
    public Guid ReviewDefinitionId { get; set; }

    /// <summary>
    /// Unique identifier of the document process.
    /// </summary>
    public Guid DocumentProcessDefinitionId { get; set; }

    /// <summary>
    /// Document process information.
    /// </summary>
    public DocumentProcessInfo? DocumentProcess { get; set; }

    /// <summary>
    /// Indicates whether the document process is active for this review definition.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
