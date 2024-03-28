using MassTransit;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.API.Main.Hubs;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Events;

namespace ProjectVico.V2.API.Main.Consumers;

public class ChatMessageResponseReceivedNotificationConsumer : IConsumer<ChatMessageResponseReceived>
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public ChatMessageResponseReceivedNotificationConsumer(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<ChatMessageResponseReceived> context)
    {
        var message = context.Message;
        var groupId = context.Message.ChatMessageDto.ConversationId.ToString();

        // We send the message to any client that has joined the same conversation
        await _hubContext.Clients.Group(groupId).SendAsync("ReceiveChatMessageResponseReceivedNotification", message);
    }
}