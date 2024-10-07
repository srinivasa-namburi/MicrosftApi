using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers;

public class ProcessChatMessageNotificationConsumer : IConsumer<ProcessChatMessage>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    public ProcessChatMessageNotificationConsumer(IHubContext<NotificationHub, INotificationHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<ProcessChatMessage> context)
    {
        var message = context.Message;
        var groupId = message.ChatMessageDto.ConversationId.ToString();

        // We send the message to any client that has joined the same conversation
        await _hubContext.Clients.Group(groupId).ReceiveProcessChatMessageReceivedNotification(message);
    }
}
