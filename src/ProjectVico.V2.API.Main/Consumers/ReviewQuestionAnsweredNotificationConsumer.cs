using MassTransit;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.API.Main.Hubs;
using ProjectVico.V2.Shared.Contracts.Messages;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Events;
using ProjectVico.V2.Shared.Hubs;

namespace ProjectVico.V2.API.Main.Consumers;

public class ReviewQuestionAnsweredNotificationConsumer : IConsumer<ReviewQuestionAnswered>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;
    public ReviewQuestionAnsweredNotificationConsumer(IHubContext<NotificationHub, INotificationHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task Consume(ConsumeContext<ReviewQuestionAnswered> context)
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