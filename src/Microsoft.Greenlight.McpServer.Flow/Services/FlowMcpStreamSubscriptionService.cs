// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Contracts.Streams;
using Orleans;
using Orleans.Streams;
using System.Collections.Concurrent;

namespace Microsoft.Greenlight.McpServer.Flow.Services;

/// <summary>
/// Background service that manages Orleans stream subscriptions for Flow MCP sessions.
/// Enables real-time coordination between backend conversations and Flow MCP responses.
/// </summary>
public class FlowMcpStreamSubscriptionService : BackgroundService
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<FlowMcpStreamSubscriptionService> _logger;

    // Track active subscriptions and their handlers
    private readonly ConcurrentDictionary<Guid, FlowSessionSubscription> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<Guid, StreamSubscriptionHandle<FlowBackendConversationUpdate>> _streamHandles = new();

    public FlowMcpStreamSubscriptionService(
        IClusterClient clusterClient,
        ILogger<FlowMcpStreamSubscriptionService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Flow MCP Stream Subscription Service started");

        // Keep the service running to maintain stream subscriptions
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Periodic cleanup of expired subscriptions
                await CleanupExpiredSubscriptionsAsync();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Flow MCP Stream Subscription Service");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Flow MCP Stream Subscription Service stopping");
    }

    /// <summary>
    /// Subscribes to Flow backend conversation updates for a specific session.
    /// </summary>
    public async Task<string> SubscribeToFlowUpdatesAsync(
        Guid sessionId,
        Func<FlowBackendConversationUpdate, Task> updateHandler,
        TimeSpan? expiry = null)
    {
        try
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var expiryTime = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(30));

            // Create subscription info
            var subscription = new FlowSessionSubscription
            {
                SubscriptionId = subscriptionId,
                SessionId = sessionId,
                ExpiresAt = expiryTime,
                UpdateHandler = updateHandler
            };

            _activeSubscriptions[sessionId] = subscription;

            // Subscribe to Orleans stream
            var streamProvider = _clusterClient.GetStreamProvider("StreamProvider");
            var stream = streamProvider.GetStream<FlowBackendConversationUpdate>(
                ChatStreamNameSpaces.FlowBackendConversationUpdateNamespace,
                sessionId);

            var streamHandle = await stream.SubscribeAsync(async (update, token) =>
            {
                try
                {
                    _logger.LogDebug("Received Flow update for session {SessionId}, conversation {ConversationId}, complete: {IsComplete}",
                        update.FlowSessionId, update.BackendConversationId, update.IsComplete);

                    await updateHandler(update);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling Flow update for session {SessionId}", sessionId);
                }
            });

            _streamHandles[sessionId] = streamHandle;

            _logger.LogInformation("Subscribed to Flow updates for session {SessionId}, expires at {ExpiryTime}",
                sessionId, expiryTime);

            return subscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to Flow updates for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Unsubscribes from Flow updates for a specific session.
    /// </summary>
    public async Task UnsubscribeFromFlowUpdatesAsync(Guid sessionId)
    {
        try
        {
            if (_streamHandles.TryRemove(sessionId, out var streamHandle))
            {
                await streamHandle.UnsubscribeAsync();
            }

            _activeSubscriptions.TryRemove(sessionId, out _);

            _logger.LogInformation("Unsubscribed from Flow updates for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from Flow updates for session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Gets current subscription status for a session.
    /// </summary>
    public FlowSessionSubscription? GetSubscription(Guid sessionId)
    {
        _activeSubscriptions.TryGetValue(sessionId, out var subscription);
        return subscription;
    }

    /// <summary>
    /// Gets all active Flow session subscriptions.
    /// </summary>
    public IReadOnlyDictionary<Guid, FlowSessionSubscription> GetActiveSubscriptions()
    {
        return _activeSubscriptions.AsReadOnly();
    }

    /// <summary>
    /// Cleans up expired subscriptions to prevent memory leaks.
    /// </summary>
    private async Task CleanupExpiredSubscriptionsAsync()
    {
        var now = DateTime.UtcNow;
        var expiredSessions = _activeSubscriptions
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in expiredSessions)
        {
            _logger.LogDebug("Cleaning up expired Flow subscription for session {SessionId}", sessionId);
            await UnsubscribeFromFlowUpdatesAsync(sessionId);
        }

        if (expiredSessions.Any())
        {
            _logger.LogInformation("Cleaned up {Count} expired Flow subscriptions", expiredSessions.Count);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Flow MCP Stream Subscription Service...");

        // Unsubscribe from all active streams
        var tasks = _streamHandles.Keys.Select(UnsubscribeFromFlowUpdatesAsync);
        await Task.WhenAll(tasks);

        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Represents an active Flow session subscription.
/// </summary>
public class FlowSessionSubscription
{
    public required string SubscriptionId { get; set; }
    public required Guid SessionId { get; set; }
    public required DateTime ExpiresAt { get; set; }
    public required Func<FlowBackendConversationUpdate, Task> UpdateHandler { get; set; }
}