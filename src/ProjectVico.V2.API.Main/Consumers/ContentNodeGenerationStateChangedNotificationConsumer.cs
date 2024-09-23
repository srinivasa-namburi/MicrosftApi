using MassTransit;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.API.Main.Hubs;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;
using ProjectVico.V2.Shared.Hubs;

namespace ProjectVico.V2.API.Main.Consumers;

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