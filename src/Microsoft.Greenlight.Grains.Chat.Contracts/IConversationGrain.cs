// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Grains.Chat.Contracts.Models;
using Microsoft.Greenlight.Grains.Chat.Contracts.State;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts;

public interface IConversationGrain : IGrainWithGuidKey
{
    Task<ConversationState> GetStateAsync();
    Task InitializeAsync(string documentProcessName, string systemPrompt);
    /// <summary>
    /// Sets or changes the document process used by this conversation and optionally updates the system prompt.
    /// </summary>
    /// <param name="documentProcessName">The document process short name.</param>
    /// <param name="updateSystemPrompt">Whether to also update the conversation's system prompt to the default for the new process.</param>
    Task<bool> SetDocumentProcessAsync(string documentProcessName, bool updateSystemPrompt = true);
    /// <summary>
    /// Sets the user context (provider subject ID) for this conversation.
    /// Used by Flow backend conversations where user messages are not directly sent.
    /// </summary>
    /// <param name="providerSubjectId">The provider subject ID of the user who started this conversation.</param>
    Task SetUserContextAsync(string providerSubjectId);
    Task<bool> RemoveConversationReference(Guid conversationReferenceId);
    Task<List<ChatMessageDTO>> GetMessagesAsync();
    Task<ChatMessageDTO> ProcessMessageAsync(ChatMessageDTO userMessage);
    Task GenerateSummaryAsync(DateTime summaryTime);

    // New method for callback
    Task OnMessageProcessingComplete(ProcessMessageResult result);
}
