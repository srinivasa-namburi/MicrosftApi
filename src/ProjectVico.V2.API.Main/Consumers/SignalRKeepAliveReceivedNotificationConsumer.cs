using MassTransit;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.API.Main.Hubs;
using ProjectVico.V2.Shared.Contracts.Messages;

namespace ProjectVico.V2.API.Main.Consumers;

public class SignalRKeepAliveReceivedNotificationConsumer : IConsumer<SignalRKeepAlive>
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRKeepAliveReceivedNotificationConsumer(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }
    public async Task Consume(ConsumeContext<SignalRKeepAlive> context)
    {
        // This message isn't consumed - it's just used to send a notification to the client to keep the SignalR connection alive
        await _hubContext.Clients.All.SendAsync("ReceiveSignalRKeepAliveReceivedNotification", context.Message);
    }
}