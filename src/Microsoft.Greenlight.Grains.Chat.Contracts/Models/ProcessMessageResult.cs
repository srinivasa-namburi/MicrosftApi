using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Grains.Chat.Contracts.Models
{
    /// <summary>
    /// Result of message processing containing the extracted references and generated response
    /// </summary>
    public class ProcessMessageResult
    {
        public ChatMessage UserMessageEntity { get; set; }
        public List<ContentReferenceItem> ExtractedReferences { get; set; }
        public ChatMessageDTO AssistantMessageDto { get; set; }
        public ChatMessage AssistantMessageEntity { get; set; }
    }
}