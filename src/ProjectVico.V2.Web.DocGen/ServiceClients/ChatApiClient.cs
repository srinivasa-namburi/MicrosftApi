using System.Net;
using Microsoft.AspNetCore.Components.Authorization;
using ProjectVico.V2.Shared.Contracts.Chat;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

internal sealed class ChatApiClient : BaseServiceClient<ChatApiClient>, IChatApiClient
{
    public ChatApiClient(HttpClient httpClient, ILogger<ChatApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<string?> SendChatMessageAsync(ChatMessageDTO chatMessageDto)
    {
        var response = await SendPostRequestMessage($"/api/chat", chatMessageDto);
        response?.EnsureSuccessStatusCode();

        return response?.StatusCode.ToString();
    }

    public async Task<List<ChatMessageDTO>> GetChatMessagesAsync(Guid conversationId)
    {
        var conversationIdString = conversationId.ToString();
        var response = await SendGetRequestMessage($"/api/chat/{conversationIdString}");

        // If we get a 404, it means that the conversation does not exist - return an empty list
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<ChatMessageDTO>();
        }

        response?.EnsureSuccessStatusCode();

        // Return chat history for the conversation if found - otherwise return an empty list
        return await response?.Content.ReadFromJsonAsync<List<ChatMessageDTO>>()! ??
               new List<ChatMessageDTO>();
    }
}