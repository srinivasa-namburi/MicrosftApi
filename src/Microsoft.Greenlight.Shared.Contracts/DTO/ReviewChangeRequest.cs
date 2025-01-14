namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a request to change a review.
/// </summary>
public class ReviewChangeRequest
{
    /// <summary>
    /// Review definition information.
    /// </summary>
    public required ReviewDefinitionInfo ReviewDefinition { get; set; }

    /// <summary>
    /// List of changed or added review questions.
    /// </summary>
    public List<ReviewQuestionInfo> ChangedOrAddedQuestions { get; set; } = [];

    /// <summary>
    /// List of deleted review questions.
    /// </summary>
    public List<ReviewQuestionInfo> DeletedQuestions { get; set; } = [];
}
