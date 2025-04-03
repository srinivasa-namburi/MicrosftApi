using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers
{
    /// <summary>
    /// Consumer for handling ChatMessageStatusNotification events.
    /// </summary>
    public class ChatMessageStatusNotificationConsumer : IConsumer<ChatMessageStatusNotification>
    {
        private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

        /// <summary>
        /// Initializes a new instance of the 
        /// <see cref="ChatMessageStatusNotificationConsumer"/> class.
        /// </summary>
        /// <param name="hubContext">The SignalR hub context.</param>
        public ChatMessageStatusNotificationConsumer(
            IHubContext<NotificationHub, INotificationHubClient> hubContext
        )
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Consumes the ChatMessageStatusNotification event and sends a notification to the appropriate SignalR group (which in
        /// this case is a message id).
        /// </summary>
        /// <param name="context">The context containing the ChatMessageStatusNotification message.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task Consume(ConsumeContext<ChatMessageStatusNotification> context)
        {
            var message = context.Message;
            var groupId = message.ChatMessageId.ToString();

            // We send the message to any client that has joined the conversation
            await _hubContext.Clients.Group(groupId).ReceiveChatMessageStatusNotification(message);
        }
    }
}