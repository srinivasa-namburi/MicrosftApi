using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers;

/// <summary>
/// Consumer class for handling ContentNodeGenerationStateChanged events.
/// </summary>
public class ContentNodeGenerationStateChangedNotificationConsumer : IConsumer<ContentNodeGenerationStateChanged>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentNodeGenerationStateChangedNotificationConsumer"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context.</param>
    public ContentNodeGenerationStateChangedNotificationConsumer(
        IHubContext<NotificationHub, INotificationHubClient> hubContext
    )
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Consumes the ContentNodeGenerationStateChanged event and sends a notification to the client.
    /// </summary>
    /// <param name="context">The context containing the ContentNodeGenerationStateChanged event message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<ContentNodeGenerationStateChanged> context)
    {
        var message = context.Message;
        // Use SignalR to send a notification to the client so the client
        // can retrieve the generated content node from the database through the API

        await _hubContext.Clients.All.ReceiveContentNodeGenerationStateChangedNotification(message);
    }
}
