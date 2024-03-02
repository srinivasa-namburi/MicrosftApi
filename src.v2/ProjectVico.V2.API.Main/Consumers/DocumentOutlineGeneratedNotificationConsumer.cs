using MassTransit;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.API.Main.Hubs;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;

namespace ProjectVico.V2.API.Main.Consumers;

public class DocumentOutlineGeneratedNotificationConsumer : IConsumer<DocumentOutlineGenerated>
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public DocumentOutlineGeneratedNotificationConsumer(IHubContext<NotificationHub> hubContext)
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

        await _hubContext.Clients.All.SendAsync("ReceiveDocumentOutlineNotification", correlationId);

        //await _hubContext.Clients.User(userId).SendAsync(
        //    "ReceiveDocumentOutlineNotification", correlationId);


    }

   
}