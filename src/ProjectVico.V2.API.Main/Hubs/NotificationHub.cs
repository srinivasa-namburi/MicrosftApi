using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.Shared.Contracts.Chat;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.Chat.Events;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;

namespace ProjectVico.V2.API.Main.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public async Task SendDocumentOutlineNotification(string userId, Guid correlationId)
    {
        await Clients.User(userId).SendAsync("ReceiveDocumentOutlineNotification", correlationId);
    }

    public async Task SendContentNodeGenerationStateChangedNotification(string userId, ContentNodeGenerationStateChanged contentNodeGenerationStateMessage)
    {
        await Clients.User(userId).SendAsync("ReceiveContentNodeGenerationStateChangedNotification", contentNodeGenerationStateMessage);
    }

    public async Task SendContentNodeNotification(string userId, ContentNodeGenerated contentNodeGenerated)
    {
        await Clients.User(userId).SendAsync("ReceiveContentNodeNotification", contentNodeGenerated);
    }

    public async Task SendChatMessageResponseReceivedNotification(string groupId, ChatMessageResponseReceived chatMessageResponseReceived)
    {
        await Clients.Groups(groupId).SendAsync("ReceiveChatMessageResponseReceivedNotification", chatMessageResponseReceived);
    }

    public async Task SendProcessChatMessageReceivedNotification(string groupId, ProcessChatMessage message)
    {
        await Clients.Groups(groupId).SendAsync("ReceiveProcessChatMessageReceivedNotification", message);
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