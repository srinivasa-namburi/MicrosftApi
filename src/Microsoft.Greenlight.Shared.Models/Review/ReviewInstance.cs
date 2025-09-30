using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Models.Review;

/// <summary>
/// Represents an instance of a review, including its definition, status, and associated answers.
/// </summary>
public class ReviewInstance : EntityBase
{
    /// <summary>
    /// Unique identifier of the review definition.
    /// </summary>
    public required Guid ReviewDefinitionId { get; set; }

    /// <summary>
    /// Review definition associated with this instance.
    /// </summary>
    [JsonIgnore]
    public ReviewDefinition? ReviewDefinition { get; set; }

    /// <summary>
    /// Unique identifier of the external link asset (file being reviewed).
    /// Nullable to support reviews without documents or during migration.
    /// </summary>
    public Guid? ExternalLinkAssetId { get; set; }

    /// <summary>
    /// External link asset associated with this instance.
    /// </summary>
    [JsonIgnore]
    public ExternalLinkAsset? ExternalLinkAsset { get; set; }

    /// <summary>
    /// Short name of the document process associated with this review instance.
    /// This is used to create the appropriate Semantic Kernel instance.
    /// </summary>
    public string? DocumentProcessShortName { get; set; }

    /// <summary>
    /// Unique identifier of the document process definition linked to this review instance.
    /// </summary>
    public Guid? DocumentProcessDefinitionId { get; set; }

    /// <summary>
    /// State of the review definition when it was submitted.
    /// </summary>
    public string? ReviewDefinitionStateWhenSubmitted { get; set; }

    /// <summary>
    /// Status of the review instance.
    /// </summary>
    public ReviewInstanceStatus Status { get; set; } = ReviewInstanceStatus.Pending;

    /// <summary>
    /// List of answers to the review questions.
    /// </summary>
    public List<ReviewQuestionAnswer> ReviewQuestionAnswers { get; set; } = [];
}
