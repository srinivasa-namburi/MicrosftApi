namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents the status of a review instance.
/// </summary>
public enum ReviewInstanceStatus
{
    /// <summary>
    /// The review instance is pending.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The review instance is in progress.
    /// </summary>
    InProgress = 100,

    /// <summary>
    /// The review instance is completed.
    /// </summary>
    Completed = 999,

    /// <summary>
    /// The review instance has failed.
    /// </summary>
    Failed = 1000
}
