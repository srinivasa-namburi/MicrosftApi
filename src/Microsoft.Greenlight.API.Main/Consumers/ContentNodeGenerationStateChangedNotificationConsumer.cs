using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers;

public class ContentNodeGenerationStateChangedNotificationConsumer : IConsumer<ContentNodeGenerationStateChanged>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    public ContentNodeGenerationStateChangedNotificationConsumer(IHubContext<NotificationHub, INotificationHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<ContentNodeGenerationStateChanged> context)
    {
        var message = context.Message;
        // Use SignalR to send a notification to the client so the client
        // can retrieve the generated content node from the database through the API

        await _hubContext.Clients.All.ReceiveContentNodeGenerationStateChangedNotification(message);
    }
}
