using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers;

public class DocumentOutlineGeneratedNotificationConsumer : IConsumer<DocumentOutlineGenerated>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    public DocumentOutlineGeneratedNotificationConsumer(IHubContext<NotificationHub, INotificationHubClient> hubContext)
    {
        _hubContext = hubContext;

    }
    public async Task Consume(ConsumeContext<DocumentOutlineGenerated> context)
    {
        // Use SignalR to send a notification to the client so the client
        // can retrieve the generated document outline from the database through the API
        // DON'T send the entire document outline in the message, just the correlation ID

        var documentOutlineGenerated = context.Message;
        var correlationId = documentOutlineGenerated.CorrelationId;

        var userId = documentOutlineGenerated.AuthorOid;

        await _hubContext.Clients.All.ReceiveDocumentOutlineNotification(correlationId);
    }
}
