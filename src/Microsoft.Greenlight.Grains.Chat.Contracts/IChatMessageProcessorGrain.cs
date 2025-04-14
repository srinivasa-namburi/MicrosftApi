using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Models;
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts;

public interface IChatMessageProcessorGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Process a chat message and generate a response without blocking the conversation grain
    /// </summary>
    /// <param name="userMessageDto">The user message to process</param>
    /// <param name="conversationId">ID of the parent conversation</param>
    /// <param name="documentProcessName">The document process name</param>
    /// <param name="systemPrompt">The system prompt to use</param>
    /// <param name="referenceItemIds">The current reference item IDs for the conversation</param>
    /// <param name="conversationMessages">The existing messages in the conversation</param>
    /// <param name="conversationSummaries">The conversation summaries</param>
    /// <returns>The processing result containing extracted references and assistant response</returns>
    Task ProcessMessageAsync(
        ChatMessageDTO userMessageDto,
        Guid conversationId,
        string documentProcessName,
        string systemPrompt,
        List<Guid> referenceItemIds,
        List<ChatMessage> conversationMessages,
        List<ConversationSummary> conversationSummaries);
}