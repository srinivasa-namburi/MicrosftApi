using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Review
{
    [StatelessWorker]
    public class ReviewNotifierGrain : Grain, IReviewNotifierGrain
    {
        private readonly ILogger<ReviewNotifierGrain> _logger;

        public ReviewNotifierGrain(ILogger<ReviewNotifierGrain> logger)
        {
            _logger = logger;
        }

        public async Task NotifyProcessingMessageAsync(BackendProcessingMessageGenerated notification)
        {
            try
            {
                _logger.LogInformation("Publishing processing message notification for review instance {InstanceId}: {Message}",
                    notification.CorrelationId, notification.Message);

                // Forward to SignalR notifier
                var signalRNotifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                await signalRNotifierGrain.NotifyBackendProcessingMessageAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing processing message notification for review instance {InstanceId}",
                    notification.CorrelationId);
            }
        }

        public async Task NotifyReviewQuestionAnsweredAsync(ReviewQuestionAnsweredNotification notification)
        {
            try
            {
                _logger.LogInformation("Publishing review question answered notification for review instance {InstanceId}",
                    notification.CorrelationId);

                // Forward to SignalR notifier
                var signalRNotifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                await signalRNotifierGrain.NotifyReviewQuestionAnsweredAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing review question answered notification for review instance {InstanceId}",
                    notification.CorrelationId);
            }
        }

        public async Task NotifyReviewCompletedAsync(Guid reviewInstanceId)
        {
            try
            {
                _logger.LogInformation("Publishing review completed notification for review instance {InstanceId}",
                    reviewInstanceId);

                // Forward to SignalR notifier
                var signalRNotifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                await signalRNotifierGrain.NotifyReviewCompletedAsync(new ReviewCompletedNotification(reviewInstanceId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing review completed notification for review instance {InstanceId}",
                    reviewInstanceId);
            }
        }
    }
}
