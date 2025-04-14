using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Shared.Contracts.Chat;

/// <summary>
/// Data Transfer Object for chat messages.
/// </summary>
[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public class ChatMessageDTO
{
    /// <summary>
    /// Unique identifier for the chat message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier of the message being replied to, if any.
    /// </summary>
    public Guid? ReplyToId { get; set; }

    /// <summary>
    /// Unique identifier for the conversation.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Source of the chat message.
    /// </summary>
    public ChatMessageSource Source { get; set; }

    /// <summary>
    /// Creation state of the chat message.
    /// </summary>
    public ChatMessageCreationState State { get; set; } = ChatMessageCreationState.Complete;

    /// <summary>
    /// Message content.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Text content of the message.
    /// </summary>
    public string? ContentText { get; set; }

    /// <summary>
    /// UTC date and time when the message was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Unique identifier of the user who sent the message.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Full name of the user who sent the message.
    /// </summary>
    public string UserFullName { get; set; } = "Unknown User";
}
