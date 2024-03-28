using MassTransit;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.API.Main.Hubs;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;

namespace ProjectVico.V2.API.Main.Consumers;

public class ContentNodeGeneratedNotificationConsumer : IConsumer<ContentNodeGenerated>
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public ContentNodeGeneratedNotificationConsumer(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<ContentNodeGenerated> context)
    {
        // Use SignalR to send a notification to the client so the client
        // can retrieve the generated content node from the database through the API
        // DON'T send the entire content node in the message, just the correlation ID

        //var contentNodeGenerated = context.Message;
        //var correlationId = contentNodeGenerated.CorrelationId;

        //var userId = contentNodeGenerated.AuthorOid;

        //await _hubContext.Clients.All.SendAsync("ReceiveContentNodeNotification", contentNodeGenerated);

        //await _hubContext.Clients.User(userId).SendAsync(
        //    "ReceiveContentNodeNotification", correlationId);
    }
}