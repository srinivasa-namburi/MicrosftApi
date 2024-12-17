using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Chat;

public class ChatMessageDTO
{
    public Guid Id { get; set; }
    public Guid? ReplyToId { get; set; }
    public Guid ConversationId { get; set; }
    public ChatMessageSource Source { get; set; }
    public ChatMessageCreationState State { get; set; } = ChatMessageCreationState.Complete;
    public string Message { get; set; }
    public string? ContentText { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public string UserFullName { get; set; } = "Unknown User";

}
