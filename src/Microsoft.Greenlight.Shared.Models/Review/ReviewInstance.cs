using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

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
    /// Unique identifier of the exported document link.
    /// </summary>
    public required Guid ExportedLinkId { get; set; }

    /// <summary>
    /// Exported document link associated with this instance.
    /// </summary>
    [JsonIgnore]
    public ExportedDocumentLink? ExportedDocumentLink { get; set; }

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
