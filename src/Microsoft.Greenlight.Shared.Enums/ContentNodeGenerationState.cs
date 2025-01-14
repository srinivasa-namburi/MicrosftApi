namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents the state of the content node generation.
/// </summary>
public enum ContentNodeGenerationState
{
    /// <summary>
    /// The content node is in an outline only state.
    /// </summary>
    OutlineOnly = 100,

    /// <summary>
    /// The content node generation is currently in progress.
    /// </summary>
    InProgress = 200,

    /// <summary>
    /// The content node generation has been completed successfully.
    /// </summary>
    Completed = 300,

    /// <summary>
    /// The content node generation has encountered a failure.
    /// </summary>
    Failed = 999
}
