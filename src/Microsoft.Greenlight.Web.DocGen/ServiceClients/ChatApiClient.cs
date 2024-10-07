using System.Net;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.ServiceClients;

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

    public async Task<List<ChatMessageDTO>> GetChatMessagesAsync(Guid conversationId, string documentProcessShortName)
    {
        var conversationIdString = conversationId.ToString();
        var response = await SendGetRequestMessage($"/api/chat/{documentProcessShortName}/{conversationIdString}");

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
