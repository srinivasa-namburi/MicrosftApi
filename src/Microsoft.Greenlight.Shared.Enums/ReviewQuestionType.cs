namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents the types of actual questions that needs to be reviewed for validity.
/// </summary>
public enum ReviewQuestionType
{
    /// <summary>
    /// Represents a direct question.
    /// </summary>
    Question = 100,

    /// <summary>
    /// Represents a requirement that needs to be formulated as a question.
    /// </summary>
    Requirement = 500
}
