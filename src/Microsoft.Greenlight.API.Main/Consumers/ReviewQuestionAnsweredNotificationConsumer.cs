using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.API.Main.Hubs;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Hubs;

namespace Microsoft.Greenlight.API.Main.Consumers;

public class ReviewQuestionAnsweredNotificationConsumer : IConsumer<ReviewQuestionAnsweredNotification>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;
    public ReviewQuestionAnsweredNotificationConsumer(IHubContext<NotificationHub, INotificationHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task Consume(ConsumeContext<ReviewQuestionAnsweredNotification> context)
    {
        var message = context.Message;
        var groupId = context.Message.CorrelationId.ToString(); // ReviewInstanceId

        // We send the message to any client holding this review instance open
        return _hubContext.Clients.Group(groupId).ReceiveReviewQuestionAnsweredNotification(message);
    }
}

public class BackendProcessingMessageGeneratedNotificationConsumer : IConsumer<BackendProcessingMessageGenerated>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;
    public BackendProcessingMessageGeneratedNotificationConsumer(IHubContext<NotificationHub, INotificationHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task Consume(ConsumeContext<BackendProcessingMessageGenerated> context)
    {
        var message = context.Message;
        var groupId = context.Message.CorrelationId.ToString(); // ReviewInstanceId

        // We send the message to any client holding an item with the Correlation ID open
        return _hubContext.Clients.Group(groupId).ReceiveBackendProcessingMessageGeneratedNotification(message);
    }
}
