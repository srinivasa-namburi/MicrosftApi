using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.Review;

/// <summary>
/// Represents the association between a review definition and a document process definition.
/// </summary>
public class ReviewDefinitionDocumentProcessDefinition : EntityBase
{
    /// <summary>
    /// Unique identifier of the review.
    /// </summary>
    public required Guid ReviewId { get; set; }

    /// <summary>
    /// Review definition associated with this document process definition.
    /// </summary>
    [JsonIgnore]
    public ReviewDefinition? Review { get; set; }

    /// <summary>
    /// Unique identifier of the document process definition.
    /// </summary>
    public required Guid DocumentProcessDefinitionId { get; set; }

    /// <summary>
    /// Dynamic document process definition associated with this review definition.
    /// </summary>
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DocumentProcessDefinition { get; set; }

    /// <summary>
    /// Indicates whether the document process definition is active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
