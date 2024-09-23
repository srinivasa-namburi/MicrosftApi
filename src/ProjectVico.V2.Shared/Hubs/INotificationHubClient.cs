using ProjectVico.V2.Shared.Contracts.Messages;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Events;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Events;

namespace ProjectVico.V2.Shared.Hubs;

public interface INotificationHubClient
{
    Task ReceiveDocumentOutlineNotification(Guid correlationId);
    Task ReceiveContentNodeGenerationStateChangedNotification(ContentNodeGenerationStateChanged contentNodeGenerationStateMessage);
    Task ReceiveContentNodeNotification(ContentNodeGenerated contentNodeGenerated);
    Task ReceiveChatMessageResponseReceivedNotification(ChatMessageResponseReceived chatMessageResponseReceived);
    Task ReceiveProcessChatMessageReceivedNotification(ProcessChatMessage message);
    Task ReceiveReviewQuestionAnsweredNotification(ReviewQuestionAnswered message);
    Task ReceiveBackendProcessingMessageGeneratedNotification(BackendProcessingMessageGenerated message);
}