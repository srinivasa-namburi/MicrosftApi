using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Hubs;
using Microsoft.Greenlight.Shared.Notifiers;

namespace Microsoft.Greenlight.API.Main.Consumers
{
    /// <summary>
    /// Consumer for handling BackendProcessingMessageGenerated notifications.
    /// </summary>
    public class BackendProcessingMessageGeneratedNotificationConsumer : IConsumer<BackendProcessingMessageGenerated>
    {
        private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendProcessingMessageGeneratedNotificationConsumer"/> class.
        /// </summary>
        /// <param name="hubContext">The SignalR hub context.</param>
        public BackendProcessingMessageGeneratedNotificationConsumer(
            IHubContext<NotificationHub, INotificationHubClient> hubContext
        )
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Consumes the BackendProcessingMessageGenerated message and sends a notification to the appropriate SignalR group.
        /// </summary>
        /// <param name="context">The context containing the BackendProcessingMessageGenerated message.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task Consume(ConsumeContext<BackendProcessingMessageGenerated> context)
        {
            var message = context.Message;
            var groupId = context.Message.CorrelationId.ToString(); // ReviewInstanceId

            // We send the message to any client holding an item with the Correlation ID open
            return _hubContext.Clients.Group(groupId).ReceiveBackendProcessingMessageGeneratedNotification(message);
        }
    }
}
