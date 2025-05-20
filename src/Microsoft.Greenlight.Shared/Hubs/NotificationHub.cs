using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;

namespace Microsoft.Greenlight.Shared.Hubs;

/// <summary>
/// Hub for sending various notifications to clients.
/// </summary>
[Authorize]
public class NotificationHub : Hub<INotificationHubClient>
{
    /// <summary>
    /// Sends a document outline notification to a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user to send the notification to.</param>
    /// <param name="correlationId">The correlation ID of the document outline.</param>
    public async Task SendDocumentOutlineNotification(string userId, Guid correlationId)
    {
        await Clients.User(userId).ReceiveDocumentOutlineNotification(correlationId.ToString());
    }

    /// <summary>
    /// Sends a content node generation state changed notification to a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user to send the notification to.</param>
    /// <param name="contentNodeGenerationStateMessage">The content node generation state message.</param>
    public async Task SendContentNodeGenerationStateChangedNotification(
        string groupId,
        ContentNodeGenerationStateChanged contentNodeGenerationStateMessage)
    {
        await Clients.Group(groupId).ReceiveContentNodeGenerationStateChangedNotification(contentNodeGenerationStateMessage);
    }

    /// <summary>
    /// Sends a content node notification to a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user to send the notification to.</param>
    /// <param name="contentNodeGenerated">The content node generated message.</param>
    public async Task SendContentNodeNotification(string userId, ContentNodeGenerated contentNodeGenerated)
    {
        await Clients.Group(userId).ReceiveContentNodeNotification(contentNodeGenerated);
    }

    /// <summary>
    /// Sends a chat message response received notification to a specific group.
    /// </summary>
    /// <param name="groupId">The ID of the group to send the notification to.</param>
    /// <param name="chatMessageResponseReceived">The chat message response received message.</param>
    public async Task SendChatMessageResponseReceivedNotification(
        string groupId,
        ChatMessageResponseReceived chatMessageResponseReceived)
    {
        await Clients.Groups(groupId).ReceiveChatMessageResponseReceivedNotification(chatMessageResponseReceived);
    }

    /// <summary>
    /// Sends a process chat message received notification to a specific group.
    /// </summary>
    /// <param name="groupId">The ID of the group to send the notification to.</param>
    /// <param name="message">The process chat message.</param>
    public async Task SendProcessChatMessageReceivedNotification(string groupId, ProcessChatMessage message)
    {
        await Clients.Groups(groupId).ReceiveProcessChatMessageReceivedNotification(message);
    }

    /// <summary>
    /// Sends a review question answered notification to a specific group.
    /// </summary>
    /// <param name="groupId">The ID of the group to send the notification to.</param>
    /// <param name="message">The review question answered message.</param>
    public async Task SendReviewQuestionAnsweredNotification(string groupId, ReviewQuestionAnsweredNotification message)
    {
        await Clients.Group(groupId).ReceiveReviewQuestionAnsweredNotification(message);
    }

    /// <summary>
    /// Sends a backend processing message generated notification to a specific group.
    /// </summary>
    /// <param name="groupId">The ID of the group to send the notification to.</param>
    /// <param name="message">The backend processing message generated.</param>
    public async Task SendBackendProcessingMessageGeneratedNotification(string groupId, BackendProcessingMessageGenerated message)
    {
        await Clients.Group(groupId).ReceiveBackendProcessingMessageGeneratedNotification(message);
    }

    /// <summary>
    /// Sends a review completed notification to a specific group.
    /// </summary>
    /// <param name="groupId">The ID of the group to send the notification to (review execution id)</param>
    /// <param name="message">The complete notification message generated</param>
    public async Task SendReviewCompletedNotification(string groupId, ReviewCompletedNotification message)
    {
        await Clients.Group(groupId).ReceiveReviewCompletedNotification(message);
    }

    /// <summary>
    /// Sends a notification that the conversation references have been updated to a specific group (conversation).
    /// </summary>
    /// <param name="groupId"></param>
    /// <param name="message"></param>
    public async Task ReceiveConversationReferencesUpdatedNotification(string groupId,
        ConversationReferencesUpdatedNotification message)
    {
        await Clients.Group(groupId).ReceiveConversationReferencesUpdatedNotification(message);
    }

    /// <summary>
    /// Sends a chat message status notification to a specific group.
    /// </summary>
    /// <param name="messageId">The ID of the message to send the notification to.</param>
    /// <param name="notification">The chat message status notification.</param>
    public async Task SendChatMessageStatusNotification(
        string messageId,
        ChatMessageStatusNotification notification)
    {
        await Clients.Group(messageId).ReceiveChatMessageStatusNotification(notification);
    }

    /// <summary>
    /// Sends a notification that a validation pipeline execution has started for a document.
    /// Scoped to the group with the generated document ID.
    /// </summary>
    /// <param name="groupId">The ID of the generated document receiving the notification</param>
    /// <param name="notification">The message containing details about the validation pipeline execution</param>
    public async Task SendValidationExecutionStartedNotification(
        string groupId, 
        ValidationExecutionForDocumentNotification notification)
    {
        await Clients.Group(groupId).ReceiveValidationExecutionForDocumentNotification(notification);
    }

    /// <summary>
    /// Sends a notification that an index export job has completed to a specific group.
    /// </summary>
    /// <param name="groupId">The group to notify (user group).</param>
    /// <param name="notification">The export job notification.</param>
    public async Task SendExportJobCompletedNotification(string groupId, IndexExportJobNotification notification)
    {
        await Clients.Group(groupId).ReceiveExportJobCompletedNotification(notification);
    }

    /// <summary>
    /// Sends a notification that an index export job has failed to a specific group.
    /// </summary>
    /// <param name="groupId">The group to notify (user group).</param>
    /// <param name="notification">The export job notification.</param>
    public async Task SendExportJobFailedNotification(string groupId, IndexExportJobNotification notification)
    {
        await Clients.Group(groupId).ReceiveExportJobFailedNotification(notification);
    }

    /// <summary>
    /// Sends a notification that an index import job has completed to a specific group.
    /// </summary>
    /// <param name="groupId">The group to notify (user group).</param>
    /// <param name="notification">The import job notification.</param>
    public async Task SendImportJobCompletedNotification(string groupId, IndexImportJobNotification notification)
    {
        await Clients.Group(groupId).ReceiveImportJobCompletedNotification(notification);
    }

    /// <summary>
    /// Sends a notification that an index import job has failed to a specific group.
    /// </summary>
    /// <param name="groupId">The group to notify (user group).</param>
    /// <param name="notification">The import job notification.</param>
    public async Task SendImportJobFailedNotification(string groupId, IndexImportJobNotification notification)
    {
        await Clients.Group(groupId).ReceiveImportJobFailedNotification(notification);
    }

    /// <summary>
    /// Adds the current connection to a specific group.
    /// </summary>
    /// <param name="groupName">The name of the group to add the connection to.</param>
    public async Task AddToGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Removes the current connection from a specific group.
    /// </summary>
    /// <param name="groupName">The name of the group to remove the connection from.</param>
    public async Task RemoveFromGroup(string groupName)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
        catch
        {
            // Do nothing - this is not critical.
        }
    }
}
