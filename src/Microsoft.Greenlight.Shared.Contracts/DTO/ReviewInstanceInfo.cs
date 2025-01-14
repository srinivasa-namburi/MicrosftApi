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
    /// Unique identifier of the exported link.
    /// </summary>
    public Guid ExportedLinkId { get; set; }

    /// <summary>
    /// State of the review definition when it was submitted.
    /// </summary>
    public string? ReviewDefinitionStateWhenSubmitted { get; set; }

    /// <summary>
    /// Status of the review instance.
    /// </summary>
    public ReviewInstanceStatus Status { get; set; } = ReviewInstanceStatus.Pending;
}
