namespace Microsoft.Greenlight.Shared.Models.Review;

/// <summary>
/// Represents the definition of a review, including questions, title, description, and associated instances.
/// </summary>
public class ReviewDefinition : EntityBase
{
    /// <summary>
    /// List of questions associated with the review.
    /// </summary>
    public List<ReviewQuestion> ReviewQuestions { get; set; } = [];

    /// <summary>
    /// Title of the review.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Description of the question.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Connections to document process definitions.
    /// </summary>
    public List<ReviewDefinitionDocumentProcessDefinition> DocumentProcessDefinitionConnections { get; set; } = [];

    /// <summary>
    /// List of review instances.
    /// </summary>
    public List<ReviewInstance> ReviewInstances { get; set; } = [];
}
