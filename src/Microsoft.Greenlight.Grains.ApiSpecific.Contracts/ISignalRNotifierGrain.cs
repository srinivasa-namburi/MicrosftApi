using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Orleans;

namespace Microsoft.Greenlight.Grains.ApiSpecific.Contracts
{
    /// <summary>
    /// Grain interface for handling SignalR notifications
    /// </summary>
    public interface ISignalRNotifierGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Document Domain Notifications
        /// </summary>

        /// <summary>
        /// Notify clients that a document outline has been generated
        /// </summary>
        /// <param name="notification">Document outline notification details</param>
        Task NotifyDocumentOutlineGeneratedAsync(DocumentOutlineGeneratedNotification notification);

        /// <summary>
        /// Notify clients about content node generation state changes
        /// </summary>
        /// <param name="notification">Content node state change notification</param>
        Task NotifyContentNodeStateChangedAsync(ContentNodeGenerationStateChanged notification);

        /// <summary>
        /// Notify clients that document outline generation has failed
        /// </summary>
        /// <param name="notification">Document outline generation failed notification</param>
        Task NotifyDocumentOutlineGenerationFailedAsync(DocumentOutlineGenerationFailed notification);

        /// <summary>
        /// Chat Domain Notifications
        /// </summary>

        /// <summary>
        /// Notify clients about chat message status changes
        /// </summary>
        /// <param name="notification">Chat message status notification</param>
        Task NotifyChatMessageStatusAsync(ChatMessageStatusNotification notification);

        /// <summary>
        /// Notify clients about chat message responses
        /// </summary>
        /// <param name="notification">Chat message response notification</param>
        Task NotifyChatMessageResponseReceivedAsync(ChatMessageResponseReceived notification);

        /// <summary>
        /// Notify clients about conversation references updates
        /// </summary>
        /// <param name="notification">Conversation references updated notification</param>
        Task NotifyConversationReferencesUpdatedAsync(ConversationReferencesUpdatedNotification notification);

        /// <summary>
        /// Notifies clients about content chunk updates.
        /// </summary>
        /// <param name="notification">The content chunk update notification.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task NotifyContentChunkUpdateAsync(ContentChunkUpdate notification);

        /// <summary>
        /// Notify clients about validation execution steps and results
        /// </summary>
        /// <param name="notification"></param>
        /// <returns></returns>
        Task NotifyValidationExecutionForDocumentAsync(ValidationExecutionForDocumentNotification notification);

        // Review domain notifications
        Task NotifyBackendProcessingMessageAsync(BackendProcessingMessageGenerated notification);
        Task NotifyReviewQuestionAnsweredAsync(ReviewQuestionAnsweredNotification notification);
        Task NotifyReviewCompletedAsync(ReviewCompletedNotification notification);
    }
}
