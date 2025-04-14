using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Orleans;

namespace Microsoft.Greenlight.Grains.Review.Contracts
{
    public interface IReviewNotifierGrain : IGrainWithGuidKey
    {
        Task NotifyProcessingMessageAsync(BackendProcessingMessageGenerated notification);
        Task NotifyReviewQuestionAnsweredAsync(ReviewQuestionAnsweredNotification notification);
        Task NotifyReviewCompletedAsync(Guid reviewInstanceId);
    }
}