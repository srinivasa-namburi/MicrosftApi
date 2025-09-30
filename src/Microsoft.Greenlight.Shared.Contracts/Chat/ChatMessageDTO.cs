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

    /// <summary>
    /// Optional metadata indicating this is an aggregation/flow synthesized message.
    /// When true, UI can render with special styling and provide a drill-down to intermediate backend sections.
    /// </summary>
    public bool IsFlowAggregation { get; set; }

    /// <summary>
    /// Indicates this message is an intermediate aggregation still receiving updates.
    /// UI may show a spinner / in-progress badge.
    /// Becomes false once final synthesized response replaces it (but message kept for history if desired).
    /// </summary>
    public bool IsIntermediate { get; set; }

    /// <summary>
    /// Optional reference to a prior message that supersedes this one (e.g., final synthesized replacing intermediate aggregation).
    /// </summary>
    public Guid? SupersededByMessageId { get; set; }

    /// <summary>
    /// Optional structured extra data (serialized JSON) containing per-backend sections or diagnostics.
    /// UI components should treat as opaque unless they understand the schema.
    /// </summary>
    public string? ExtraDataJson { get; set; }

    /// <summary>
    /// List of message IDs that this message supersedes (for final synthesis messages in Flow).
    /// Used to show which intermediate messages were replaced by this final message.
    /// </summary>
    public List<Guid>? SupersededMessageIds { get; set; }
}
