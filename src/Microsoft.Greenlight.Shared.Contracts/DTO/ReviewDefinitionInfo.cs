namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents the definition of a review, including its questions and metadata.
/// </summary>
public class ReviewDefinitionInfo
{
    /// <summary>
    /// Unique identifier for the review definition.
    /// </summary>
    public required Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// List of review questions.
    /// </summary>
    public List<ReviewQuestionInfo> ReviewQuestions { get; set; } = [];

    /// <summary>
    /// List of document processes associated with this review definition.
    /// </summary>
    public List<ReviewDefinitionDocumentProcessInfo> DocumentProcesses { get; set; } = [];

    /// <summary>
    /// Title of the review definition.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Description of the review definition.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Determines whether the specified object is equal to the other <see cref="ReviewDefinitionInfo"/> object using ID.
    /// </summary>
    /// <param name="obj">The object to compare with the other object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not ReviewDefinitionInfo other)
            return false;

        return Id == other.Id;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    /// Returns a string that represents the current object (Title if present; otherwise, empty string).
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return Title;
    }
}
