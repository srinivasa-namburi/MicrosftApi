// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.McpServer.Flow.Contracts.Requests;
using Microsoft.Greenlight.McpServer.Flow.Contracts.Responses;
using Microsoft.Greenlight.McpServer.Flow.Services;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using ModelContextProtocol.Server;
using Orleans;
using static Microsoft.Greenlight.Grains.Chat.Contracts.FlowSessionStatus;

namespace Microsoft.Greenlight.McpServer.Flow.Tools;

/// <summary>
/// Enhanced Flow MCP tools with Orleans streams integration for real-time coordination.
/// Provides streaming query processing and real-time status updates.
/// </summary>
[McpServerToolType]
public static class StreamingFlowTools
{
    private const string FlowConversationIdKey = "FlowConversationId";

    /// <summary>
    /// Starts a streaming Flow query that processes across multiple document processes
    /// and provides real-time updates via Orleans streams.
    /// </summary>
    [McpServerTool(Name = "flow_query_stream"),
     Description("Start a streaming Flow query that orchestrates multiple document processes. " +
                 "Returns immediately with a subscription ID for real-time updates. " +
                 "Use flow_query_status to check progress and get results.")]
    public static async Task<FlowQueryStreamResponse> StartStreamingQueryAsync(
        McpRequestContext requestContext,
        IClusterClient clusterClient,
        IMcpSessionManager sessionManager,
        FlowMcpStreamSubscriptionService streamService,
        ILogger<FlowQueryRequest> logger,
        FlowQueryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Resolve or create Flow conversation ID
            var (success, flowConversationGuid, errorMessage) = await ResolveFlowConversationIdAsync(
                requestContext, sessionManager, clusterClient, request.flowConversationId, logger, cancellationToken);

            if (!success)
            {
                return new FlowQueryStreamResponse
                {
                    status = "error",
                    error = errorMessage ?? "Unable to resolve Flow conversation"
                };
            }

            // Get Flow orchestration grain
            var flowGrain = clusterClient.GetGrain<IFlowOrchestrationGrain>(flowConversationGuid);

            // Start processing (non-blocking)
            var processingTaskId = await flowGrain.StartStreamingMessageProcessingAsync(request.message, request.context ?? string.Empty);

            // Set up stream subscription for real-time updates
            string? subscriptionId = null;
            if (request.acceptStreamingUpdates)
            {
                subscriptionId = await streamService.SubscribeToFlowUpdatesAsync(
                    flowConversationGuid,
                    async update => await HandleFlowUpdateAsync(update, flowConversationGuid),
                    TimeSpan.FromMinutes(30)); // 30-minute subscription expiry
            }

            return new FlowQueryStreamResponse
            {
                flowConversationId = flowConversationGuid.ToString(),
                subscriptionId = subscriptionId,
                processingTaskId = processingTaskId,
                status = "processing",
                message = request.acceptStreamingUpdates
                    ? "Your query is being processed with streaming updates enabled. You will receive push notifications as progress is made. Use flow_query_status for detailed status checks."
                    : "Your query is being processed across multiple document processes. Use flow_query_status to check progress.",
                streamingUpdatesEnabled = request.acceptStreamingUpdates
            };
        }
        catch (Exception ex)
        {
            return new FlowQueryStreamResponse
            {
                status = "error",
                error = ex.Message,
                streamingUpdatesEnabled = request.acceptStreamingUpdates
            };
        }
    }

    /// <summary>
    /// Gets the current status and results of a streaming Flow query.
    /// </summary>
    [McpServerTool(Name = "flow_query_status"),
     Description("Check the status and get results of a streaming Flow query. " +
                 "Provide the sessionId from the original flow_query_stream call.")]
    public static async Task<FlowQueryStatusResponse> GetQueryStatusAsync(
        McpRequestContext requestContext,
        IClusterClient clusterClient,
        IMcpSessionManager sessionManager,
        FlowMcpStreamSubscriptionService streamService,
        ILogger<FlowQueryRequest> logger,
        FlowQueryStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Resolve Flow conversation ID
            var (success, flowConversationGuid, errorMessage) = await ResolveFlowConversationIdAsync(
                requestContext, sessionManager, clusterClient, request.flowConversationId, logger, cancellationToken);

            if (!success)
            {
                return new FlowQueryStatusResponse
                {
                    status = "error",
                    error = errorMessage ?? "Unable to resolve Flow conversation"
                };
            }

            // Get current Flow session state
            var flowGrain = clusterClient.GetGrain<IFlowOrchestrationGrain>(flowConversationGuid);
            var sessionState = await flowGrain.GetStateAsync();

            // Get subscription status
            var subscription = streamService.GetSubscription(flowConversationGuid);

            // Get the actual response from the grain
            string? currentResponse = sessionState.CurrentResponse;
            string? intermediateResponse = null;

            if (sessionState.Status == Completed)
            {
                // Store the intermediate/working response
                intermediateResponse = sessionState.CurrentResponse;

                // Get the actual messages to find the final assistant response
                var messages = await flowGrain.GetMessagesAsync();
                var lastAssistantMessage = messages
                    .Where(m => m.Source.ToString().ToLowerInvariant() == "assistant" && !m.IsIntermediate)
                    .OrderByDescending(m => m.CreatedUtc)
                    .FirstOrDefault();

                if (lastAssistantMessage != null && !string.IsNullOrEmpty(lastAssistantMessage.Message))
                {
                    currentResponse = lastAssistantMessage.Message;
                }
            }

            return new FlowQueryStatusResponse
            {
                flowConversationId = flowConversationGuid.ToString(),
                status = MapFlowSessionStatus(sessionState.Status),
                progress = CalculateProgress(sessionState),
                engagedProcesses = sessionState.EngagedDocumentProcesses,
                activeConversationIds = sessionState.ActiveConversationIds.Select(id => id.ToString()).ToList(),
                currentResponse = currentResponse,
                intermediateResponse = intermediateResponse,
                isComplete = sessionState.Status == Completed,
                subscriptionActive = subscription != null && subscription.ExpiresAt > DateTime.UtcNow,
                subscriptionExpiresAt = subscription?.ExpiresAt,
                lastActivityUtc = sessionState.LastActivityUtc
            };
        }
        catch (Exception ex)
        {
            return new FlowQueryStatusResponse
            {
                status = "error",
                error = ex.Message
            };
        }
    }

    /// <summary>
    /// Cancels a streaming Flow query and cleans up resources.
    /// </summary>
    [McpServerTool(Name = "flow_query_cancel"),
     Description("Cancel a streaming Flow query and clean up resources. " +
                 "Provide the sessionId from the original flow_query_stream call.")]
    public static async Task<FlowQueryCancelResponse> CancelQueryAsync(
        McpRequestContext requestContext,
        IClusterClient clusterClient,
        IMcpSessionManager sessionManager,
        FlowMcpStreamSubscriptionService streamService,
        ILogger<FlowQueryRequest> logger,
        FlowQueryCancelRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Resolve Flow conversation ID
            var (success, flowConversationGuid, errorMessage) = await ResolveFlowConversationIdAsync(
                requestContext, sessionManager, clusterClient, request.flowConversationId, logger, cancellationToken);

            if (!success)
            {
                return new FlowQueryCancelResponse
                {
                    status = "error",
                    error = errorMessage ?? "Unable to resolve Flow conversation"
                };
            }

            // Cancel processing in Flow grain
            var flowGrain = clusterClient.GetGrain<IFlowOrchestrationGrain>(flowConversationGuid);
            await flowGrain.CancelProcessingAsync();

            // Unsubscribe from streams
            await streamService.UnsubscribeFromFlowUpdatesAsync(flowConversationGuid);

            return new FlowQueryCancelResponse
            {
                flowConversationId = flowConversationGuid.ToString(),
                status = "cancelled",
                message = "Flow query has been cancelled and resources cleaned up."
            };
        }
        catch (Exception ex)
        {
            return new FlowQueryCancelResponse
            {
                status = "error",
                error = ex.Message
            };
        }
    }

    /// <summary>
    /// Handles Flow backend conversation updates received via Orleans streams.
    /// </summary>
    private static async Task HandleFlowUpdateAsync(FlowBackendConversationUpdate update, Guid sessionGuid)
    {
        // This method handles real-time updates from backend conversations
        // In a more sophisticated implementation, this could:
        // 1. Update a shared cache with partial results
        // 2. Trigger intermediate response synthesis
        // 3. Publish to WebSocket clients for real-time UI updates
        // 4. Store conversation progress for status queries

        // For now, we'll just log the update
        // Real implementation would store these updates for retrieval via flow_query_status
        await Task.CompletedTask;
    }

    /// <summary>
    /// Resolves or creates a Flow conversation ID.
    /// If flowConversationId is provided, validates it exists and updates MCP session mapping.
    /// If not provided, gets from MCP session mapping or creates new.
    /// </summary>
    /// <returns>Tuple of (success, flowConversationGuid, errorMessage)</returns>
    private static async Task<(bool Success, Guid FlowConversationGuid, string? ErrorMessage)> ResolveFlowConversationIdAsync(
        McpRequestContext requestContext,
        IMcpSessionManager sessionManager,
        IClusterClient clusterClient,
        string? flowConversationId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Get Greenlight session ID from context
        var greenlightSessionId = requestContext.GreenlightSessionId;

        logger.LogDebug("StreamingFlowTools.ResolveFlowConversationIdAsync: Greenlight session ID from context: {GreenlightSessionId}, flowConversationId param: {FlowConversationId}",
            greenlightSessionId ?? "null", flowConversationId ?? "null");

        // If Flow conversation ID provided explicitly, validate and use it
        if (!string.IsNullOrEmpty(flowConversationId))
        {
            if (!Guid.TryParse(flowConversationId, out var conversationGuid))
            {
                return (false, Guid.Empty, $"Invalid Flow conversation ID format: {flowConversationId}");
            }

            // Check if the conversation exists by trying to get its state
            var flowGrain = clusterClient.GetGrain<IFlowOrchestrationGrain>(conversationGuid);
            var state = await flowGrain.GetStateAsync();

            if (state == null || state.SessionId == Guid.Empty)
            {
                return (false, Guid.Empty, $"Flow conversation not found: {conversationGuid}");
            }

            // Update Greenlight session mapping to track this Flow conversation
            if (!string.IsNullOrEmpty(greenlightSessionId) && !string.IsNullOrWhiteSpace(requestContext.ServerNamespace))
            {
                await sessionManager.SetSessionDataAsync(requestContext.ServerNamespace, greenlightSessionId, FlowConversationIdKey, conversationGuid.ToString(), cancellationToken);
            }

            return (true, conversationGuid, null);
        }

        // No Flow conversation ID provided - get from Greenlight session or create new
        Guid? flowGuid = null;
        if (!string.IsNullOrEmpty(greenlightSessionId) && !string.IsNullOrWhiteSpace(requestContext.ServerNamespace))
        {
            logger.LogDebug("StreamingFlowTools: Getting Flow conversation ID from Greenlight session {GreenlightSessionId}", greenlightSessionId);
            var storedValue = await sessionManager.GetSessionDataAsync(requestContext.ServerNamespace, greenlightSessionId, FlowConversationIdKey, cancellationToken);
            logger.LogDebug("StreamingFlowTools: Retrieved stored value from session: {StoredValue}", storedValue ?? "null");

            if (!string.IsNullOrEmpty(storedValue) && Guid.TryParse(storedValue, out var parsedGuid))
            {
                flowGuid = parsedGuid;
                logger.LogInformation("StreamingFlowTools: Using existing Flow conversation {FlowConversationId} from Greenlight session {GreenlightSessionId}",
                    flowGuid, greenlightSessionId);
            }
            else
            {
                // No Flow conversation in session yet - create a new one
                flowGuid = Guid.NewGuid();
                logger.LogInformation("StreamingFlowTools: Creating new Flow conversation {FlowConversationId} for Greenlight session {GreenlightSessionId}",
                    flowGuid, greenlightSessionId);
                await sessionManager.SetSessionDataAsync(requestContext.ServerNamespace, greenlightSessionId, FlowConversationIdKey, flowGuid.Value.ToString(), cancellationToken);
            }
        }
        else
        {
            logger.LogWarning("StreamingFlowTools: No Greenlight session ID available - cannot persist Flow conversation mapping");
        }

        if (flowGuid.HasValue && flowGuid.Value != Guid.Empty)
        {
            return (true, flowGuid.Value, null);
        }

        // Fallback: create a new Flow conversation (should rarely happen)
        var newConversationGuid = Guid.NewGuid();
        logger.LogWarning("StreamingFlowTools: Fallback - creating new Flow conversation {FlowConversationId} without MCP session mapping", newConversationGuid);
        return (true, newConversationGuid, null);
    }

    /// <summary>
    /// Maps Flow session status to public API status.
    /// </summary>
    private static string MapFlowSessionStatus(FlowSessionStatus status)
    {
        return status switch
        {
            Created => "created",
            Processing => "processing",
            Completed => "completed",
            Error => "error",
            Cancelled => "cancelled",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Calculates processing progress as a percentage.
    /// </summary>
    private static int CalculateProgress(FlowSessionState sessionState)
    {
        if (sessionState.Status == Completed)
            return 100;

        if (sessionState.Status == Processing)
        {
            // Simple heuristic based on engaged processes and active conversations
            if (sessionState.EngagedDocumentProcesses.Count == 0)
                return 10; // Intent detection complete

            if (sessionState.ActiveConversationIds.Count == 0)
                return 30; // Processes selected

            // Estimate based on conversation activity
            return Math.Min(90, 30 + (sessionState.ActiveConversationIds.Count * 20));
        }

        return 0;
    }
}

