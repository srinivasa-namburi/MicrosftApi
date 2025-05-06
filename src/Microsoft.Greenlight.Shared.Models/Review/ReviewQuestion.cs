using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.Review;

/// <summary>
/// Represents a question in a review, including the question text, rationale, type, and associated review.
/// </summary>
public class ReviewQuestion : EntityBase
{
    /// <summary>
    /// Text of the question.
    /// </summary>
    public required string Question { get; set; }

    /// <summary>
    /// Rationale for the question.
    /// </summary>
    public string? Rationale { get; set; }

    /// <summary>
    /// Type of the question.
    /// </summary>
    public ReviewQuestionType QuestionType { get; set; }

    /// <summary>
    /// Order of the question within the review. Lower values appear first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Unique identifier of the associated review.
    /// </summary>
    public Guid ReviewId { get; set; }

    /// <summary>
    /// Associated review definition.
    /// </summary>
    [JsonIgnore]
    public ReviewDefinition? Review { get; set; }
}
