// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.Chat.Commands;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IChatApiClient : IServiceClient
{
    //IAsyncEnumerable<string> SendChatMessage(ChatMessage chatMessage);
    Task<string?> SendChatMessageAsync(ChatMessageDTO chatMessageDto);
    Task<List<ChatMessageDTO>> GetChatMessagesAsync(Guid conversationId, string documentProcessShortName);
    /// <summary>
    /// Updates the document process for an existing conversation.
    /// </summary>
    Task SetConversationDocumentProcessAsync(Guid conversationId, SetConversationDocumentProcessRequest request);
}
