using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Contracts.Streams;
using Orleans.Streams;

namespace Microsoft.Greenlight.Shared.Notifiers;

/// <summary>
/// Flow orchestration stream notifier for handling conversation monitoring events
/// </summary>
public class FlowWorkStreamNotifier : IWorkStreamNotifier
{
    private readonly ILogger<FlowWorkStreamNotifier> _logger;
    private readonly IClusterClient _clusterClient;

    public string Name => "Flow";

    public FlowWorkStreamNotifier(
        ILogger<FlowWorkStreamNotifier> logger,
        IClusterClient clusterClient)
    {
        _logger = logger;
        _clusterClient = clusterClient;
    }

    /// <inheritdoc />
    public async Task<List<object>> SubscribeToStreamsAsync(
        IClusterClient clusterClient,
        IStreamProvider streamProvider)
    {
        var subscriptionHandles = new List<object>();

        try
        {
            // Subscribe to chat message responses to monitor backend conversations
            var chatResponseSubscription = await streamProvider
                .GetStream<ChatMessageResponseReceived>(ChatStreamNameSpaces.ChatMessageResponseReceivedNamespace, Guid.Empty)
                .SubscribeAsync(new FlowConversationMonitorObserver(_clusterClient, _logger));

            subscriptionHandles.Add(chatResponseSubscription);
            _logger.LogInformation("Subscribed to chat message response notifications for Flow monitoring");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Flow stream subscription");
            throw;
        }

        return subscriptionHandles;
    }

    /// <inheritdoc />
    public async Task UnsubscribeFromStreamsAsync(IEnumerable<object> subscriptionHandles)
    {
        foreach (var handle in subscriptionHandles)
        {
            if (handle is StreamSubscriptionHandle<ChatMessageResponseReceived> chatHandle)
            {
                await chatHandle.UnsubscribeAsync();
            }
        }
    }
}

/// <summary>
/// Observer for Flow conversation monitoring events
/// </summary>
public class FlowConversationMonitorObserver : IAsyncObserver<ChatMessageResponseReceived>
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger _logger;

    public FlowConversationMonitorObserver(
        IClusterClient clusterClient,
        ILogger logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public Task OnCompletedAsync() => Task.CompletedTask;

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in FlowConversationMonitorObserver");
        return Task.CompletedTask;
    }

    public async Task OnNextAsync(ChatMessageResponseReceived notification, StreamSequenceToken? token = null)
    {
        try
        {
            _logger.LogDebug("Received chat message response notification for conversation {ConversationId}",
                notification.ChatMessageDto.ConversationId);

            // Find any Flow orchestration grains that might be monitoring this conversation
            // We need a way to map backend conversation IDs to Flow session IDs
            // For now, we'll use a simple approach where Flow grains subscribe to specific conversation updates

            // This is a placeholder - in a real implementation, we'd need a registry or mapping
            // of which Flow sessions are monitoring which backend conversations

            await Task.CompletedTask; // Placeholder for actual Flow notification logic
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling chat message response in Flow monitor for correlation ID: {CorrelationId}",
                notification.CorrelationId);
        }
    }
}