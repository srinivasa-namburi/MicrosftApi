using ProjectVico.V2.Shared.Contracts.Chat;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IChatApiClient : IServiceClient
{
    //IAsyncEnumerable<string> SendChatMessage(ChatMessage chatMessage);
    Task<string?> SendChatMessageAsync(ChatMessageDTO chatMessageDto);
    Task<List<ChatMessageDTO>> GetChatMessagesAsync(Guid conversationId, string documentProcessShortName);
}