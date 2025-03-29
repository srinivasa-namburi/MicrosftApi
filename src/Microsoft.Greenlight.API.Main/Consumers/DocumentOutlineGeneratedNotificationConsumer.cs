using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers;

/// <summary>
/// Consumer class for handling DocumentOutlineGenerated events.
/// </summary>
public class DocumentOutlineGeneratedNotificationConsumer : IConsumer<DocumentOutlineGeneratedNotification>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentOutlineGeneratedNotificationConsumer"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context.</param>
    public DocumentOutlineGeneratedNotificationConsumer(
        IHubContext<NotificationHub, INotificationHubClient> hubContext
    )
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Consumes the DocumentOutlineGenerated event and sends a notification to the client.
    /// </summary>
    /// <param name="context">The context containing the DocumentOutlineGenerated message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<DocumentOutlineGeneratedNotification> context)
    {
        var documentOutlineGenerated = context.Message;
        var correlationId = documentOutlineGenerated.CorrelationId;
        var correlationIdString = correlationId.ToString();

        // Send to the specific group with this correlation ID
        await _hubContext.Clients.Group(correlationIdString)
            .ReceiveDocumentOutlineNotification(correlationIdString);
        
        // Also send to all clients as a fallback
        await _hubContext.Clients.All
            .ReceiveDocumentOutlineNotification(correlationIdString);
    }
}