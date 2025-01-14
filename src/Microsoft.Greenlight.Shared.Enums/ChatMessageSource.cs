namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents the source of a chat message.
/// </summary>
public enum ChatMessageSource
{
    /// <summary>
    /// The message is from a user.
    /// </summary>
    User,

    /// <summary>
    /// The message is from an assistant.
    /// </summary>
    Assistant,

    /// <summary>
    /// The message is from the system.
    /// </summary>
    System
}
