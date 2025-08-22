using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Reindexing.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;

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
    /// Receives a notification that a review execution has been completed.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    Task ReceiveReviewCompletedNotification(ReviewCompletedNotification message);

    /// <summary>
    /// Receives a conversation references updated notification.
    /// </summary>
    /// <param name="message">The conversation references updated message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveConversationReferencesUpdatedNotification(ConversationReferencesUpdatedNotification message);

    /// <summary>
    /// Sends a chat message status notification to a specific group.
    /// </summary>
    /// <param name="notification">The chat message status notification.</param>
    Task ReceiveChatMessageStatusNotification(ChatMessageStatusNotification notification);

    /// <summary>
    /// Receives a content chunk update notification.
    /// </summary>
    /// <param name="contentChunkUpdate">The content chunk update.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveContentChunkUpdateNotification(ContentChunkUpdate contentChunkUpdate);

    /// <summary>
    /// Sends a notification that a validation pipeline execution has reached a certain state for a document.
    /// </summary>
    /// <param name="notification"></param>
    /// <returns></returns>
    Task ReceiveValidationExecutionForDocumentNotification(
        ValidationExecutionForDocumentNotification notification);

    /// <summary>
    /// Receives a notification that an index export job has been completed.
    /// </summary>
    /// <param name="notification">The index export job notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveExportJobCompletedNotification(IndexExportJobNotification notification);

    /// <summary>
    /// Receives a notification that an index export job has failed.
    /// </summary>
    /// <param name="notification">The index export job notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveExportJobFailedNotification(IndexExportJobNotification notification);

    /// <summary>
    /// Receives a notification that an index import job has been completed.
    /// </summary>
    /// <param name="notification">The index import job notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveImportJobCompletedNotification(IndexImportJobNotification notification);

    /// <summary>
    /// Receives a notification that an index import job has failed.
    /// </summary>
    /// <param name="notification">The index import job notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveImportJobFailedNotification(IndexImportJobNotification notification);

    /// <summary>
    /// Receives a notification that document reindexing has started.
    /// </summary>
    /// <param name="notification">The document reindex started notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveDocumentReindexStartedNotification(DocumentReindexStartedNotification notification);

    /// <summary>
    /// Receives a notification about document reindexing progress.
    /// </summary>
    /// <param name="notification">The document reindex progress notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveDocumentReindexProgressNotification(DocumentReindexProgressNotification notification);

    /// <summary>
    /// Receives a notification that document reindexing has completed.
    /// </summary>
    /// <param name="notification">The document reindex completed notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveDocumentReindexCompletedNotification(DocumentReindexCompletedNotification notification);

    /// <summary>
    /// Receives a notification that document reindexing has failed.
    /// </summary>
    /// <param name="notification">The document reindex failed notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveDocumentReindexFailedNotification(DocumentReindexFailedNotification notification);
}
