using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

namespace Microsoft.Greenlight.Shared.Hubs;

/// <summary>
/// Interface for Notification Hub Client to handle various notifications.
/// </summary>
public interface INotificationHubClient
{
    /// <summary>
    /// Receives a document outline notification.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the Document Outline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveDocumentOutlineNotification(string correlationId);

    /// <summary>
    /// Receives a content node generation state changed notification.
    /// </summary>
    /// <param name="contentNodeGenerationStateMessage">The content node generation state message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveContentNodeGenerationStateChangedNotification(ContentNodeGenerationStateChanged contentNodeGenerationStateMessage);

    /// <summary>
    /// Receives a content node generated notification.
    /// </summary>
    /// <param name="contentNodeGenerated">The content node generated message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveContentNodeNotification(ContentNodeGenerated contentNodeGenerated);

    /// <summary>
    /// Receives a chat message response received notification.
    /// </summary>
    /// <param name="chatMessageResponseReceived">The chat message response received message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveChatMessageResponseReceivedNotification(ChatMessageResponseReceived chatMessageResponseReceived);

    /// <summary>
    /// Receives a process chat message received notification.
    /// </summary>
    /// <param name="message">The process chat message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveProcessChatMessageReceivedNotification(ProcessChatMessage message);

    /// <summary>
    /// Receives a review question answered notification.
    /// </summary>
    /// <param name="message">The review question answered notification message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveReviewQuestionAnsweredNotification(ReviewQuestionAnsweredNotification message);

    /// <summary>
    /// Receives a backend processing message generated notification.
    /// </summary>
    /// <param name="message">The backend processing message generated.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveBackendProcessingMessageGeneratedNotification(BackendProcessingMessageGenerated message);

    /// <summary>
    /// Receives a conversation references updated notification.
    /// </summary>
    /// <param name="message">The conversation references updated message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveConversationReferencesUpdatedNotification(ConversationReferencesUpdatedNotification message);

    /// <summary>
    /// Sends a chat message status notification to a specific group.
    /// </summary>
    /// <param name="messageId">The ID of the message to send the notification to.</param>
    /// <param name="notification">The chat message status notification.</param>
    Task ReceiveChatMessageStatusNotification(ChatMessageStatusNotification notification);

}
