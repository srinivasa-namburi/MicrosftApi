using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.Shared.Contracts.Messages;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Events;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Events;
using ProjectVico.V2.Shared.Hubs;

namespace ProjectVico.V2.API.Main.Hubs;

[Authorize]
public class NotificationHub : Hub<INotificationHubClient>
{
    public async Task SendDocumentOutlineNotification(string userId, Guid correlationId)
    {
        await Clients.User(userId).ReceiveDocumentOutlineNotification(correlationId);
    }

    public async Task SendContentNodeGenerationStateChangedNotification(string userId, ContentNodeGenerationStateChanged contentNodeGenerationStateMessage)
    {
        await Clients.User(userId).ReceiveContentNodeGenerationStateChangedNotification(contentNodeGenerationStateMessage);
    }

    public async Task SendContentNodeNotification(string userId, ContentNodeGenerated contentNodeGenerated)
    {
        await Clients.User(userId).ReceiveContentNodeNotification(contentNodeGenerated);
    }

    public async Task SendChatMessageResponseReceivedNotification(string groupId, ChatMessageResponseReceived chatMessageResponseReceived)
    {
        await Clients.Groups(groupId).ReceiveChatMessageResponseReceivedNotification(chatMessageResponseReceived);
    }

    public async Task SendProcessChatMessageReceivedNotification(string groupId, ProcessChatMessage message)
    {
        await Clients.Groups(groupId).ReceiveProcessChatMessageReceivedNotification(message);
    }

    public async Task SendReviewQuestionAnsweredNotification(string groupId, ReviewQuestionAnswered message)
    {
        await Clients.Group(groupId).ReceiveReviewQuestionAnsweredNotification(message);
    }

    public async Task SendBackendProcessingMessageGeneratedNotification(string groupId, BackendProcessingMessageGenerated message)
    {
        await Clients.Group(groupId).ReceiveBackendProcessingMessageGeneratedNotification(message);
    }

    public async Task AddToGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task RemoveFromGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

}