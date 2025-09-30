// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net.Http.Json;
using System.Net;
using Microsoft.Greenlight.Shared.Contracts.Chat.Commands;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class ChatApiClient : WebAssemblyBaseServiceClient<ChatApiClient>, IChatApiClient
{
    public ChatApiClient(HttpClient httpClient, ILogger<ChatApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<string?> SendChatMessageAsync(ChatMessageDTO chatMessageDto, bool? useFlow = null)
    {
        var url = "/api/chat";
        if (useFlow == true)
        {
            url += "?useFlow=true";
        }

        var response = await SendPostRequestMessage(url, chatMessageDto);
        response?.EnsureSuccessStatusCode();

        return response?.StatusCode.ToString();
    }

    public async Task<List<ChatMessageDTO>> GetChatMessagesAsync(Guid conversationId, string documentProcessShortName, bool? useFlow = null)
    {
        var conversationIdString = conversationId.ToString();
        var url = $"/api/chat/{documentProcessShortName}/{conversationIdString}";

        if (useFlow == true)
        {
            url += "?useFlow=true";
        }

        var response = await SendGetRequestMessage(url);

        // If we get a 404, it means that the conversation does not yet have any messages in it (but has been created)
        // and we return an empty list
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<ChatMessageDTO>();
        }

        response?.EnsureSuccessStatusCode();

        // Return chat history for the conversation if found - otherwise return an empty list
        return await response?.Content.ReadFromJsonAsync<List<ChatMessageDTO>>()! ??
               new List<ChatMessageDTO>();
    }

    public async Task SetConversationDocumentProcessAsync(Guid conversationId, SetConversationDocumentProcessRequest request)
    {
        var response = await SendPostRequestMessage($"/api/chat/conversation/{conversationId}/document-process", request);
        response?.EnsureSuccessStatusCode();
    }
}
