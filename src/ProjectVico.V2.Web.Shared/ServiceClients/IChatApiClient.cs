using ProjectVico.V2.Shared.Contracts.Chat;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IChatApiClient : IServiceClient
{
    //IAsyncEnumerable<string> SendChatMessage(ChatMessage chatMessage);
    Task<string?> SendChatMessage(ChatMessageDTO chatMessageDto);
    Task<List<ChatMessageDTO>> GetChatMessages(Guid conversationId);
}