using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents information about a review instance.
/// </summary>
public record ReviewInstanceInfo
{
    /// <summary>
    /// Unique identifier of the review instance.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique identifier of the review definition.
    /// </summary>
    public Guid ReviewDefinitionId { get; set; }

    /// <summary>
    /// Unique identifier of the external link asset (file being reviewed).
    /// Nullable to support reviews without documents or during migration.
    /// </summary>
    public Guid? ExternalLinkAssetId { get; set; }

    /// <summary>
    /// State of the review definition when it was submitted.
    /// </summary>
    public string? ReviewDefinitionStateWhenSubmitted { get; set; }

    /// <summary>
    /// Status of the review instance.
    /// </summary>
    public ReviewInstanceStatus Status { get; set; } = ReviewInstanceStatus.Pending;

    /// <summary>
    /// Short name of the document process associated with this review instance.
    /// </summary>
    public string? DocumentProcessShortName { get; set; }

    /// <summary>
    /// Unique identifier of the document process definition linked to this review instance.
    /// </summary>
    public Guid? DocumentProcessDefinitionId { get; set; }
}
