using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Hubs;
using Microsoft.Greenlight.Shared.Notifiers;

namespace Microsoft.Greenlight.API.Main.Consumers;

/// <summary>
/// Consumer for handling ReviewQuestionAnsweredNotification messages.
/// </summary>
public class ReviewQuestionAnsweredNotificationConsumer : IConsumer<ReviewQuestionAnsweredNotification>
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewQuestionAnsweredNotificationConsumer"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context.</param>
    public ReviewQuestionAnsweredNotificationConsumer(
        IHubContext<NotificationHub, INotificationHubClient> hubContext
    )
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Consumes the ReviewQuestionAnsweredNotification message and sends it to the appropriate SignalR group.
    /// </summary>
    /// <param name="context">The consume context containing the message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Consume(ConsumeContext<ReviewQuestionAnsweredNotification> context)
    {
        var message = context.Message;
        var groupId = context.Message.CorrelationId.ToString(); // ReviewInstanceId

        // We send the message to any client holding this review instance open
        return _hubContext.Clients.Group(groupId).ReceiveReviewQuestionAnsweredNotification(message);
    }
}
