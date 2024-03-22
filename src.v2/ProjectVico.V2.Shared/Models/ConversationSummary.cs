using System.ComponentModel;

namespace ProjectVico.V2.Shared.Models;

public class ConversationSummary : EntityBase
{
    public Guid ConversationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> SummarizedChatMessages { get; set; } = new();
    public string? SummaryText { get; set; }
}

