using MassTransit;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.API.Main.Hubs;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Commands;

namespace ProjectVico.V2.API.Main.Consumers;

public class ProcessChatMessageNotificationConsumer : IConsumer<ProcessChatMessage>
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public ProcessChatMessageNotificationConsumer(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<ProcessChatMessage> context)
    {
        var message = context.Message;
        var groupId = message.ChatMessageDto.ConversationId.ToString();

        // We send the message to any client that has joined the same conversation
        await _hubContext.Clients.Group(groupId).SendAsync("ReceiveProcessChatMessageReceivedNotification", message);
    }
}