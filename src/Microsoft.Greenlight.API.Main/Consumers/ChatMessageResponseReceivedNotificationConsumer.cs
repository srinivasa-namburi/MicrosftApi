using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers;

/// <summary>
/// Consumer for handling ChatMessageResponseReceived events.
/// </summary>
public class ChatMessageResponseReceivedNotificationConsumer : IConsumer<ChatMessageResponseReceived>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    /// <summary>
    /// Initializes a new instance of the 
    /// <see cref="ChatMessageResponseReceivedNotificationConsumer"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context.</param>
    public ChatMessageResponseReceivedNotificationConsumer(
        IHubContext<NotificationHub, INotificationHubClient> hubContext
    )
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Consumes the ChatMessageResponseReceived event and sends a notification to the appropriate SignalR group.
    /// </summary>
    /// <param name="context">The context containing the ChatMessageResponseReceived message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<ChatMessageResponseReceived> context)
    {
        var message = context.Message;
        var groupId = context.Message.ChatMessageDto.ConversationId.ToString();

        // We send the message to any client that has joined the same conversation
        await _hubContext.Clients.Group(groupId).ReceiveChatMessageResponseReceivedNotification(message);
    }
}
