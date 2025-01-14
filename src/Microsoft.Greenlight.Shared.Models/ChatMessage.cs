using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a chat message within a conversation.
/// </summary>
public class ChatMessage : EntityBase
{
    /// <summary>
    /// Chat message that this message is replying to.
    /// </summary>
    [JsonIgnore]
    public ChatMessage? ReplyToChatMessage { get; set; }

    /// <summary>
    /// ID of the chat message that this message is replying to.
    /// </summary>
    public Guid? ReplyToChatMessageId { get; set; }

    /// <summary>
    /// Conversation that this message belongs to.
    /// </summary>
    public ChatConversation? Conversation { get; set; }

    /// <summary>
    /// Unique ID of the conversation that this message belongs to.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Source of the chat message.
    /// </summary>
    public ChatMessageSource Source { get; set; }

    /// <summary>
    /// Message content of the chat message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Text content of the chat message.
    /// </summary>
    public string? ContentText { get; set; }

    /// <summary>
    /// Chat message summarized by a conversation summary.
    /// </summary>
    public ConversationSummary? SummarizedByConversationSummary { get; set; }

    /// <summary>
    /// Unique ID of the chat message summarized by a conversation summary.
    /// </summary>
    public Guid? SummarizedByConversationSummaryId { get; set; }

    /// <summary>
    /// User information of the author of the message.
    /// </summary>
    public UserInformation? AuthorUserInformation { get; set; }

    /// <summary>
    /// Unique ID of the user information of the author of the message.
    /// </summary>
    public Guid? AuthorUserInformationId { get; set; }
}
