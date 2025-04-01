using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers
{
    /// <summary>
    /// Consumer for handling ConversationReferencesUpdated events.
    /// </summary>
    public class ConversationReferencesUpdatedNotificationConsumer : IConsumer<ConversationReferencesUpdatedNotification>
    {
        private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

        /// <summary>
        /// Initializes a new instance of the 
        /// <see cref="ConversationReferencesUpdatedNotificationConsumer"/> class.
        /// </summary>
        /// <param name="hubContext">The SignalR hub context.</param>
        public ConversationReferencesUpdatedNotificationConsumer(
            IHubContext<NotificationHub, INotificationHubClient> hubContext
        )
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Consumes the ConversationReferencesUpdated event and sends a notification to the appropriate SignalR group.
        /// </summary>
        /// <param name="context">The context containing the ConversationReferencesUpdated message.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task Consume(ConsumeContext<ConversationReferencesUpdatedNotification> context)
        {
            var message = context.Message;
            var groupId = message.ConversationId.ToString();

            // We send the message to any client that has joined the same conversation
            await _hubContext.Clients.Group(groupId).ReceiveConversationReferencesUpdatedNotification(message);
        }
    }
}