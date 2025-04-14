using Microsoft.Greenlight.Grains.Chat.Contracts.Models;
using Microsoft.Greenlight.Grains.Chat.Contracts.State;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts;

public interface IConversationGrain : IGrainWithGuidKey
{
    Task<ConversationState> GetStateAsync();
    Task InitializeAsync(string documentProcessName, string systemPrompt);
    Task<bool> RemoveConversationReference(Guid conversationReferenceId);
    Task<List<ChatMessageDTO>> GetMessagesAsync();
    Task<ChatMessageDTO> ProcessMessageAsync(ChatMessageDTO userMessage);
    Task GenerateSummaryAsync(DateTime summaryTime);

    // New method for callback
    Task OnMessageProcessingComplete(ProcessMessageResult result);
}