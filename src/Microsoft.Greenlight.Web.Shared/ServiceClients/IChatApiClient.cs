using Microsoft.Greenlight.Shared.Contracts.Chat;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IChatApiClient : IServiceClient
{
    //IAsyncEnumerable<string> SendChatMessage(ChatMessage chatMessage);
    Task<string?> SendChatMessageAsync(ChatMessageDTO chatMessageDto);
    Task<List<ChatMessageDTO>> GetChatMessagesAsync(Guid conversationId, string documentProcessShortName);
}
