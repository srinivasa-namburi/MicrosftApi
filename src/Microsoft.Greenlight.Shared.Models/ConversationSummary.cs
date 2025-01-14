namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a summary of a conversation, including summarized chat messages and summary text.
/// </summary>
public class ConversationSummary : EntityBase
{
    /// <summary>
    /// Unique identifier for the conversation.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Date and time when the conversation summary was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// List of chat messages that have been summarized.
    /// </summary>
    public List<ChatMessage> SummarizedChatMessages { get; set; } = [];

    /// <summary>
    /// Text summary of the conversation.
    /// </summary>
    public string? SummaryText { get; set; }
}

