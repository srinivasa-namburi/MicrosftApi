namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents the status of a document process.
/// </summary>
public enum DocumentProcessStatus
{
    /// <summary>
    /// The document process has been created.
    /// </summary>
    Created = 0,

    /// <summary>
    /// The document process is active.
    /// </summary>
    Active = 100,

    /// <summary>
    /// The document process is disabled.
    /// </summary>
    Disabled = 999
}
