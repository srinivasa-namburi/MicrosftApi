using System.Text.Json.Serialization;
using ProjectVico.V2.Shared.Models.Enums;

namespace ProjectVico.V2.Shared.Models;

public class ChatMessage : EntityBase
{
    [JsonIgnore]
    public ChatMessage? ReplyToChatMessage { get; set; }
    public Guid? ReplyToChatMessageId { get; set; }
    
    public ChatConversation Conversation { get; set; }
    public Guid ConversationId { get; set; }

    public ChatMessageSource Source { get; set; }
    public string Message { get; set; }
    public string? ContentText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ConversationSummary? SummarizedByConversationSummary { get; set; }
    public Guid? SummarizedByConversationSummaryId { get; set; }
    public UserInformation? AuthorUserInformation { get; set; }
    public Guid? AuthorUserInformationId { get; set; }
}