// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.McpServer.Contracts.Requests;
using Microsoft.Greenlight.McpServer.Contracts.Responses;
using Microsoft.Greenlight.McpServer.Services;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Enums;
using ModelContextProtocol.Server;
using Orleans;

namespace Microsoft.Greenlight.McpServer.Tools;

/// <summary>
/// MCP tools for Flow conversational AI interactions.
/// Provides query orchestration across multiple document processes.
/// </summary>
[McpServerToolType]
public static class FlowTools
{
    /// <summary>
    /// Processes a conversational query through the Flow orchestration system.
    /// Returns a comprehensive response with full conversation history, content references,
    /// and session metadata - all materialized without requiring additional queries.
    /// </summary>
    [McpServerTool(Name = "query"),
     Description("Process a conversational query with intelligent orchestration across document processes. " +
                 "Returns comprehensive response including full conversation history, content references with " +
                 "time-limited proxy URLs, and session metadata. All data is materialized in the response.")]
    public static async Task<FlowQueryResponse> QueryAsync(
        IHttpContextAccessor httpContextAccessor,
        IClusterClient clusterClient,
        IMcpSessionManager sessionManager,
        IContentRelinkerService relinkerService,
        ILogger<FlowQueryRequest> logger,
        FlowQueryRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Resolve or create session
            var sessionId = await ResolveSessionIdAsync(httpContextAccessor, sessionManager, request.sessionId, cancellationToken);
            if (string.IsNullOrEmpty(sessionId))
            {
                return new FlowQueryResponse
                {
                    Status = "error",
                    Error = "Unable to establish session",
                    ResponseGeneratedUtc = DateTime.UtcNow
                };
            }

            logger.LogInformation("Resolved Flow session {FlowSessionId} for MCP request (provided: {ProvidedSessionId})",
                sessionId, request.sessionId ?? "none");

            // Get or create Flow orchestration grain using session ID as grain key
            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                return new FlowQueryResponse
                {
                    Status = "error",
                    Error = "Invalid session ID format",
                    ResponseGeneratedUtc = DateTime.UtcNow
                };
            }

            var flowGrain = clusterClient.GetGrain<IFlowOrchestrationGrain>(sessionGuid);

            // Initialize the grain with user information if this is a new session
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userOid = httpContext.User.FindFirst("sub")?.Value ??
                             httpContext.User.FindFirst("oid")?.Value ??
                             "anonymous";
                var userName = httpContext.User.FindFirst("name")?.Value ??
                              httpContext.User.Identity.Name;

                await flowGrain.InitializeAsync(userOid, userName);
            }

            // Process the query through the Flow orchestration using MCP-specific method
            // This method waits for all backends to complete before returning
            logger.LogInformation("Processing MCP Flow query for Flow session {SessionId} - will wait for completion", sessionId);

            // Call the MCP-specific method that waits for all backends
            // Pass timeout in seconds (Orleans can't serialize CancellationToken)
            const int timeoutSeconds = 60;
            var response = await flowGrain.ProcessQueryForMcpAsync(
                request.message,
                request.context ?? string.Empty,
                timeoutSeconds);

            // Get complete conversation history
            var messages = await flowGrain.GetMessagesAsync();

            // Get session state
            var sessionState = await flowGrain.GetStateAsync();

            // Convert messages to Flow DTOs with content references
            var conversationHistory = await ConvertMessagesToFlowDTOs(messages, relinkerService, logger);

            // Extract content references from messages for MCP
            // This only happens for MCP queries, not for UI queries
            var currentReferences = await ExtractMcpContentReferences(response, messages, relinkerService, logger);

            // Build session metadata
            var sessionMetadata = BuildSessionMetadata(sessionState, sessionId);

            stopwatch.Stop();

            return new FlowQueryResponse
            {
                Response = response.Response,
                SessionId = sessionId,
                ConversationHistory = conversationHistory,
                CurrentReferences = currentReferences,
                SessionMetadata = sessionMetadata,
                BackendConversationIds = response.ConversationIds.Select(id => id.ToString()).ToList(),
                Status = response.Status,
                IsPartial = false,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ResponseGeneratedUtc = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["queryLength"] = request.message?.Length ?? 0,
                    ["responseLength"] = response.Response?.Length ?? 0,
                    ["backendCount"] = response.ConversationIds.Count,
                    ["isMcpQuery"] = true,
                    ["contentReferenceCount"] = currentReferences.Count
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Flow query for session {SessionId}", request.sessionId);
            stopwatch.Stop();

            return new FlowQueryResponse
            {
                Status = "error",
                Error = ex.Message,
                ErrorDetails = new Dictionary<string, string>
                {
                    ["type"] = ex.GetType().Name,
                    ["stackTrace"] = ex.StackTrace ?? string.Empty
                },
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ResponseGeneratedUtc = DateTime.UtcNow
            };
        }
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
    /// Converts chat messages to Flow DTOs with content references.
    /// </summary>
    private static async Task<List<FlowMessageDTO>> ConvertMessagesToFlowDTOs(
        List<ChatMessageDTO> messages,
        IContentRelinkerService relinkerService,
        ILogger<FlowQueryRequest> logger)
    {
        var flowMessages = new List<FlowMessageDTO>();

        foreach (var msg in messages)
        {
            try
            {
                // Extract any content references from the message
                var references = await ExtractMessageReferences(msg, relinkerService, logger);

                var flowMessage = new FlowMessageDTO
                {
                    Id = msg.Id.ToString(),
                    Content = msg.Message ?? string.Empty,
                    Source = msg.Source.ToString().ToLowerInvariant(),
                    CreatedUtc = msg.CreatedUtc,
                    ModifiedUtc = null, // ChatMessageDTO doesn't have ModifiedUtc
                    ReplyToId = msg.ReplyToId?.ToString(),
                    IsFlowAggregation = msg.IsFlowAggregation,
                    IsIntermediate = msg.IsIntermediate,
                    SupersededById = msg.SupersededByMessageId?.ToString(),
                    ContentReferences = references,
                    Metadata = new Dictionary<string, object>
                    {
                        ["state"] = msg.State.ToString()
                    }
                };

                // Add contributing processes if this is an aggregation
                if (msg.IsFlowAggregation && !string.IsNullOrEmpty(msg.ExtraDataJson))
                {
                    try
                    {
                        var extraData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(msg.ExtraDataJson);
                        if (extraData?.ContainsKey("sections") == true)
                        {
                            flowMessage.Metadata["aggregationSections"] = extraData["sections"];
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to parse extra data for message {MessageId}", msg.Id);
                    }
                }

                flowMessages.Add(flowMessage);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error converting message {MessageId} to Flow DTO", msg.Id);
            }
        }

        return flowMessages;
    }

    /// <summary>
    /// Extracts content references from a message.
    /// </summary>
    private static async Task<List<FlowContentReferenceDTO>> ExtractMessageReferences(
        ChatMessageDTO message,
        IContentRelinkerService relinkerService,
        ILogger<FlowQueryRequest> logger)
    {
        var references = new List<FlowContentReferenceDTO>();

        // TODO: Implement actual reference extraction from message content
        // This would parse the message for citations, links, document references, etc.
        // For now, return empty list

        return await relinkerService.ProcessReferencesWithProxyAsync(references);
    }

    /// <summary>
    /// Extracts content references from messages for MCP clients.
    /// This is only done for MCP-initiated queries to avoid overhead for UI queries.
    /// </summary>
    private static async Task<List<FlowContentReferenceDTO>> ExtractMcpContentReferences(
        FlowQueryResult response,
        List<ChatMessageDTO> messages,
        IContentRelinkerService relinkerService,
        ILogger<FlowQueryRequest> logger)
    {
        var references = new List<FlowContentReferenceDTO>();

        try
        {
            // TODO: Implement actual content reference extraction
            // This would:
            // 1. Parse assistant messages for citations like [1], [2], etc.
            // 2. Extract URLs and document references from message content
            // 3. Query backend conversations for their source documents
            // 4. Build FlowContentReferenceDTO objects with metadata

            // For now, we're returning an empty list
            // When implemented, this would extract references from:
            // - The latest assistant message
            // - Any aggregation metadata
            // - Backend conversation citations

            logger.LogDebug("Content reference extraction for MCP will be implemented when citation patterns are defined");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error extracting content references for MCP");
        }

        // Process any found references with proxy URLs
        return await relinkerService.ProcessReferencesWithProxyAsync(references);
    }

    /// <summary>
    /// Builds session metadata from Flow session state.
    /// </summary>
    private static FlowSessionMetadataDTO BuildSessionMetadata(
        FlowSessionState sessionState,
        string sessionId)
    {
        return new FlowSessionMetadataDTO
        {
            SessionId = sessionId,
            CreatedUtc = sessionState.CreatedUtc,
            LastActivityUtc = sessionState.LastActivityUtc,
            QueryCount = sessionState.QueryCount,
            MessageCount = sessionState.QueryCount * 2, // Approximate
            UserInfo = string.IsNullOrEmpty(sessionState.UserOid) ? null : new FlowUserInfoDTO
            {
                UserId = sessionState.UserOid,
                DisplayName = sessionState.UserName
            },
            EngagedDocumentProcesses = sessionState.EngagedDocumentProcesses,
            ActiveBackendConversations = sessionState.ActiveConversationIds.Select(id => id.ToString()).ToList(),
            AvailablePlugins = sessionState.AvailablePlugins,
            ActiveCapabilities = sessionState.ActiveCapabilities,
            Status = sessionState.Status.ToString().ToLowerInvariant(),
            Metadata = new Dictionary<string, object>
            {
                ["flowSessionId"] = sessionState.SessionId.ToString()
            }
        };
    }
}