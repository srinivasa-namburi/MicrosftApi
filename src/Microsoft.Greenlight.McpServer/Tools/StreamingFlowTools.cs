// Copyright (c) Microsoft Corporation. All rights reserved.

using System.ComponentModel;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.McpServer.Contracts.Requests;
using Microsoft.Greenlight.McpServer.Contracts.Responses;
using Microsoft.Greenlight.McpServer.Services;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using ModelContextProtocol.Server;
using Orleans;
using static Microsoft.Greenlight.Grains.Chat.Contracts.FlowSessionStatus;

namespace Microsoft.Greenlight.McpServer.Tools;

/// <summary>
/// Enhanced Flow MCP tools with Orleans streams integration for real-time coordination.
/// Provides streaming query processing and real-time status updates.
/// </summary>
[McpServerToolType]
public static class StreamingFlowTools
{
    /// <summary>
    /// Starts a streaming Flow query that processes across multiple document processes
    /// and provides real-time updates via Orleans streams.
    /// </summary>
    [McpServerTool(Name = "flow_query_stream"),
     Description("Start a streaming Flow query that orchestrates multiple document processes. " +
                 "Returns immediately with a subscription ID for real-time updates. " +
                 "Use flow_query_status to check progress and get results.")]
    public static async Task<FlowQueryStreamResponse> StartStreamingQueryAsync(
        IHttpContextAccessor httpContextAccessor,
        IClusterClient clusterClient,
        IMcpSessionManager sessionManager,
        FlowMcpStreamSubscriptionService streamService,
        FlowQueryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Resolve or create session
            var sessionId = await ResolveSessionIdAsync(httpContextAccessor, sessionManager, request.sessionId, cancellationToken);
            if (string.IsNullOrEmpty(sessionId) || !Guid.TryParse(sessionId, out var sessionGuid))
            {
                return new FlowQueryStreamResponse
                {
                    status = "error",
                    error = "Unable to establish valid session"
                };
            }

            // Get Flow orchestration grain
            var flowGrain = clusterClient.GetGrain<IFlowOrchestrationGrain>(sessionGuid);

            // Start processing (non-blocking)
            var processingTaskId = await flowGrain.StartProcessingAsync(request.message, request.context ?? string.Empty);

            // Set up stream subscription for real-time updates
            var subscriptionId = await streamService.SubscribeToFlowUpdatesAsync(
                sessionGuid,
                async update => await HandleFlowUpdateAsync(update, sessionGuid),
                TimeSpan.FromMinutes(30) // 30-minute subscription expiry
            );

            return new FlowQueryStreamResponse
            {
                sessionId = sessionId,
                subscriptionId = subscriptionId,
                processingTaskId = processingTaskId,
                status = "processing",
                message = "Your query is being processed across multiple document processes. Use flow_query_status to check progress."
            };
        }
        catch (Exception ex)
        {
            return new FlowQueryStreamResponse
            {
                status = "error",
                error = ex.Message
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
        IHttpContextAccessor httpContextAccessor,
        IClusterClient clusterClient,
        IMcpSessionManager sessionManager,
        FlowMcpStreamSubscriptionService streamService,
        FlowQueryStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Resolve session ID using the same pattern as query
            var sessionId = await ResolveSessionIdAsync(httpContextAccessor, sessionManager, request.sessionId, cancellationToken);
            if (string.IsNullOrEmpty(sessionId))
            {
                return new FlowQueryStatusResponse
                {
                    status = "error",
                    error = "Unable to establish session"
                };
            }

            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                return new FlowQueryStatusResponse
                {
                    status = "error",
                    error = "Invalid session ID format"
                };
            }

            // Get current Flow session state
            var flowGrain = clusterClient.GetGrain<IFlowOrchestrationGrain>(sessionGuid);
            var sessionState = await flowGrain.GetStateAsync();

            // Get subscription status
            var subscription = streamService.GetSubscription(sessionGuid);

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
                sessionId = sessionId,
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
        IHttpContextAccessor httpContextAccessor,
        IClusterClient clusterClient,
        IMcpSessionManager sessionManager,
        FlowMcpStreamSubscriptionService streamService,
        FlowQueryCancelRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Resolve session ID using the same pattern as query
            var sessionId = await ResolveSessionIdAsync(httpContextAccessor, sessionManager, request.sessionId, cancellationToken);
            if (string.IsNullOrEmpty(sessionId))
            {
                return new FlowQueryCancelResponse
                {
                    status = "error",
                    error = "Unable to establish session"
                };
            }

            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                return new FlowQueryCancelResponse
                {
                    status = "error",
                    error = "Invalid session ID format"
                };
            }

            // Cancel processing in Flow grain
            var flowGrain = clusterClient.GetGrain<IFlowOrchestrationGrain>(sessionGuid);
            await flowGrain.CancelProcessingAsync();

            // Unsubscribe from streams
            await streamService.UnsubscribeFromFlowUpdatesAsync(sessionGuid);

            return new FlowQueryCancelResponse
            {
                sessionId = sessionId,
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
    /// Resolves or creates a session ID for the Flow conversation.
    /// Maps MCP session ID to Flow conversation ID via session manager.
    /// </summary>
    private static async Task<string?> ResolveSessionIdAsync(
        IHttpContextAccessor httpContextAccessor,
        IMcpSessionManager sessionManager,
        string? providedSessionId,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        string? mcpSessionId = providedSessionId;

        // If no session ID provided in request, try to extract from headers
        if (string.IsNullOrEmpty(mcpSessionId) && httpContext != null)
        {
            // Check both possible header names
            if (httpContext.Request.Headers.TryGetValue("X-MCP-Session", out var headerValue))
            {
                mcpSessionId = headerValue.FirstOrDefault();
            }
            else if (httpContext.Request.Headers.TryGetValue("Mcp-Session-Id", out var altHeaderValue))
            {
                mcpSessionId = altHeaderValue.FirstOrDefault();
            }
        }

        // If we have an MCP session ID (from request or headers), map it to Flow conversation
        if (!string.IsNullOrEmpty(mcpSessionId))
        {
            // Try to get or create the mapping for this MCP session
            var flowSessionId = await sessionManager.GetOrCreateFlowSessionAsync(mcpSessionId, httpContext?.User, cancellationToken);
            if (flowSessionId.HasValue)
            {
                return flowSessionId.Value.ToString();
            }
        }

        // Create new session if none provided or mapping failed
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var newSession = await sessionManager.CreateAsync(httpContext.User, cancellationToken);
            return newSession.SessionId.ToString();
        }

        return null;
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

