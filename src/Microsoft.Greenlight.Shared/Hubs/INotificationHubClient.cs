using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

namespace Microsoft.Greenlight.Shared.Hubs;

public interface INotificationHubClient
{
    Task ReceiveDocumentOutlineNotification(Guid correlationId);
    Task ReceiveContentNodeGenerationStateChangedNotification(ContentNodeGenerationStateChanged contentNodeGenerationStateMessage);
    Task ReceiveContentNodeNotification(ContentNodeGenerated contentNodeGenerated);
    Task ReceiveChatMessageResponseReceivedNotification(ChatMessageResponseReceived chatMessageResponseReceived);
    Task ReceiveProcessChatMessageReceivedNotification(ProcessChatMessage message);
    Task ReceiveReviewQuestionAnsweredNotification(ReviewQuestionAnsweredNotification message);
    Task ReceiveBackendProcessingMessageGeneratedNotification(BackendProcessingMessageGenerated message);
}
