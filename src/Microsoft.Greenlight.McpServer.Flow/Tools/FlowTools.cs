// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.McpServer.Flow.Contracts.Requests;
using Microsoft.Greenlight.McpServer.Flow.Contracts.Responses;
using Microsoft.Greenlight.McpServer.Flow.Services;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Enums;
using ModelContextProtocol.Server;
using Orleans;

namespace Microsoft.Greenlight.McpServer.Flow.Tools;

/// <summary>
/// MCP tools for Flow conversational AI interactions.
/// Provides query orchestration across multiple document processes.
/// </summary>
[McpServerToolType]
public static class FlowTools
{
    private const string FlowConversationIdKey = "FlowConversationId";
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
        McpRequestContext requestContext,
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
            // Resolve or create Flow conversation ID
            var (success, flowConversationGuid, errorMessage) = await ResolveFlowConversationIdAsync(
                requestContext, sessionManager, clusterClient, request.flowConversationId, logger, cancellationToken);

            if (!success)
            {
                return new FlowQueryResponse
                {
                    Status = "error",
                    Error = errorMessage ?? "Unable to resolve Flow conversation",
                    ResponseGeneratedUtc = DateTime.UtcNow
                };
            }

            logger.LogInformation("Resolved Flow conversation {FlowConversationId} for MCP request (provided: {ProvidedId})",
                flowConversationGuid, request.flowConversationId ?? "none");

            var flowGrain = clusterClient.GetGrain<IFlowOrchestrationGrain>(flowConversationGuid);

            // Initialize the grain with user information from context
            if (requestContext.User?.Identity?.IsAuthenticated == true)
            {
                var providerSubjectId = requestContext.ProviderSubjectId ?? "anonymous";
                var userName = requestContext.User.FindFirst("name")?.Value ??
                              requestContext.User.Identity.Name;

                await flowGrain.InitializeAsync(providerSubjectId, userName);
            }

            // Process the query through the Flow orchestration using MCP-specific method
            // This method waits for all backends to complete before returning
            logger.LogInformation("Processing MCP Flow query for Flow conversation {FlowConversationId} - will wait for completion", flowConversationGuid);

            // Call the MCP-specific method that waits for all backends
            // Pass timeout in seconds (Orleans can't serialize CancellationToken)
            const int timeoutSeconds = 60;
            var response = await flowGrain.ProcessMessageForMcpAsync(
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
            var sessionMetadata = BuildSessionMetadata(sessionState, flowConversationGuid.ToString());

            stopwatch.Stop();

            return new FlowQueryResponse
            {
                Response = response.Response,
                FlowConversationId = flowConversationGuid.ToString(),
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
            logger.LogError(ex, "Error processing Flow query for Flow conversation {FlowConversationId}", request.flowConversationId);
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

        logger.LogDebug("FlowTools.ResolveFlowConversationIdAsync: Greenlight session ID from context: {GreenlightSessionId}, flowConversationId param: {FlowConversationId}",
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
            logger.LogDebug("FlowTools: Getting Flow conversation ID from Greenlight session {GreenlightSessionId}", greenlightSessionId);
            var storedValue = await sessionManager.GetSessionDataAsync(requestContext.ServerNamespace, greenlightSessionId, FlowConversationIdKey, cancellationToken);
            logger.LogDebug("FlowTools: Retrieved stored value from session: {StoredValue}", storedValue ?? "null");

            if (!string.IsNullOrEmpty(storedValue) && Guid.TryParse(storedValue, out var parsedGuid))
            {
                flowGuid = parsedGuid;
                logger.LogInformation("FlowTools: Using existing Flow conversation {FlowConversationId} from Greenlight session {GreenlightSessionId}",
                    flowGuid, greenlightSessionId);
            }
            else
            {
                // No Flow conversation in session yet - create a new one
                flowGuid = Guid.NewGuid();
                logger.LogInformation("FlowTools: Creating new Flow conversation {FlowConversationId} for Greenlight session {GreenlightSessionId}",
                    flowGuid, greenlightSessionId);
                await sessionManager.SetSessionDataAsync(requestContext.ServerNamespace, greenlightSessionId, FlowConversationIdKey, flowGuid.Value.ToString(), cancellationToken);
            }
        }
        else
        {
            logger.LogWarning("FlowTools: No Greenlight session ID available - cannot persist Flow conversation mapping");
        }

        if (flowGuid.HasValue && flowGuid.Value != Guid.Empty)
        {
            return (true, flowGuid.Value, null);
        }

        // Fallback: create a new Flow conversation (should rarely happen)
        var newConversationGuid = Guid.NewGuid();
        logger.LogWarning("FlowTools: Fallback - creating new Flow conversation {FlowConversationId} without MCP session mapping", newConversationGuid);
        return (true, newConversationGuid, null);
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
                // Strip [NODISPLAY] content from the message
                var cleanedContent = StripNoDisplayContent(msg.Message);

                // Skip messages that are entirely [NODISPLAY]
                if (string.IsNullOrWhiteSpace(cleanedContent))
                {
                    logger.LogDebug("Skipping message {MessageId} - contains only [NODISPLAY] content", msg.Id);
                    continue;
                }

                // Extract any content references from the message
                var references = await ExtractMessageReferences(msg, relinkerService, logger);

                var flowMessage = new FlowMessageDTO
                {
                    Id = msg.Id.ToString(),
                    Content = cleanedContent,
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

    /// <summary>
    /// Strips [NODISPLAY] tagged content from a message.
    /// Content between [NODISPLAY] and [/NODISPLAY] tags is removed.
    /// </summary>
    /// <param name="message">The message to filter.</param>
    /// <returns>The message with [NODISPLAY] content removed.</returns>
    private static string StripNoDisplayContent(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        // Remove all [NODISPLAY]...[/NODISPLAY] blocks
        var result = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"\[NODISPLAY\](.*?)\[/NODISPLAY\]",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return result.Trim();
    }
}
