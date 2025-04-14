using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents information about a review question.
/// </summary>
[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public class ReviewQuestionInfo
{
    /// <summary>
    /// Unique identifier of the review question.
    /// </summary>
    public required Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Question text.
    /// </summary>
    public required string Question { get; set; }

    /// <summary>
    /// Rationale for the question.
    /// </summary>
    public string? Rationale { get; set; }

    /// <summary>
    /// Unique identifier of the review.
    /// </summary>
    public required Guid ReviewId { get; set; }

    /// <summary>
    /// Type of the review question.
    /// </summary>
    public ReviewQuestionType QuestionType { get; set; }

    /// <summary>
    /// Determines whether the specified object is equal to the current <see cref="ReviewQuestionInfo"/> object by
    /// using the properties of <see cref="ReviewQuestionInfo"/>.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not ReviewQuestionInfo other)
            return false;

        return Id == other.Id &&
               Question == other.Question &&
               Rationale == other.Rationale &&
               ReviewId == other.ReviewId &&
               QuestionType == other.QuestionType;
    }

    /// <summary>
    /// Serves as the default hash function by using the properties of <see cref="ReviewQuestionInfo"/>.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Question, Rationale, ReviewId, QuestionType);
    }
}
