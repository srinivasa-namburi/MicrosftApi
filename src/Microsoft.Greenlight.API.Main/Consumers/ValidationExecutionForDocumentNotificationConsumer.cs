using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.Hubs;
using Microsoft.Greenlight.Shared.Notifiers;

namespace Microsoft.Greenlight.API.Main.Consumers
{
    /// <summary>
    /// Consumer class for handling ValidationExecutionStartedForDocumentNotification events.
    /// </summary>
    public class ValidationExecutionForDocumentNotificationConsumer : IConsumer<ValidationExecutionForDocumentNotification>
    {
        private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationExecutionForDocumentNotificationConsumer"/> class.
        /// </summary>
        /// <param name="hubContext"></param>
        public ValidationExecutionForDocumentNotificationConsumer(
            IHubContext<NotificationHub, INotificationHubClient> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Consumes the ValidationExecutionForDocumentNotification event and sends a notification to the client.
        /// Sends to the specific group with the generated document ID.
        /// </summary>
        /// <param name="context"></param>
        public async Task Consume(ConsumeContext<ValidationExecutionForDocumentNotification> context)
        {
            var validationExecutionStarted = context.Message;
            var generatedDocumentId = validationExecutionStarted.GeneratedDocumentId;
            var generatedDocumentIdString = generatedDocumentId.ToString();

            await _hubContext.Clients.Group(generatedDocumentIdString)
                .ReceiveValidationExecutionForDocumentNotification(validationExecutionStarted);

        }
    }
}