using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers;

/// <summary>
/// Consumer class for processing chat message notifications.
/// </summary>
public class ProcessChatMessageNotificationConsumer : IConsumer<ProcessChatMessage>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessChatMessageNotificationConsumer"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context.</param>
    public ProcessChatMessageNotificationConsumer(
        IHubContext<NotificationHub, INotificationHubClient> hubContext
    )
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Consumes the chat message and sends a notification to the appropriate group.
    /// </summary>
    /// <param name="context">The context containing the ProcessChatMessage message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<ProcessChatMessage> context)
    {
        var message = context.Message;
        var groupId = message.ChatMessageDto.ConversationId.ToString();

        // We send the message to any client that has joined the same conversation
        await _hubContext.Clients.Group(groupId).ReceiveProcessChatMessageReceivedNotification(message);
    }
}
