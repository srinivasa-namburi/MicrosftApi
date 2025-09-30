// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.McpServer.Contracts.Responses;

/// <summary>
/// Comprehensive response model for Flow query operations.
/// This response materializes ALL relevant data including conversation history,
/// content references, and session metadata to provide MCP clients with complete
/// information without requiring additional queries.
/// </summary>
public record FlowQueryResponse
{
    /// <summary>
    /// The unified response from the Flow orchestration for the current query.
    /// </summary>
    [JsonPropertyName("response")]
    public string Response { get; init; } = string.Empty;

    /// <summary>
    /// The session ID for this Flow conversation.
    /// Clients should include this in subsequent requests for conversation continuity.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Complete conversation history for this session.
    /// Includes all messages exchanged, with their content references refreshed
    /// with new time-limited proxy URLs.
    /// </summary>
    [JsonPropertyName("conversationHistory")]
    public List<FlowMessageDTO> ConversationHistory { get; init; } = new();

    /// <summary>
    /// All content references from the current response.
    /// These are documents, citations, or sources used in generating this specific response.
    /// URLs are proxied through the relinker service with 10-minute expiration.
    /// </summary>
    [JsonPropertyName("currentReferences")]
    public List<FlowContentReferenceDTO> CurrentReferences { get; init; } = new();

    /// <summary>
    /// Comprehensive session metadata including engaged processes, capabilities, and state.
    /// </summary>
    [JsonPropertyName("sessionMetadata")]
    public FlowSessionMetadataDTO SessionMetadata { get; init; } = new();

    /// <summary>
    /// Backend conversation IDs that were involved in generating this response.
    /// For debugging and tracing purposes.
    /// </summary>
    [JsonPropertyName("backendConversationIds")]
    public List<string> BackendConversationIds { get; init; } = new();

    /// <summary>
    /// Status of the query processing (e.g., "completed", "processing", "error").
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "completed";

    /// <summary>
    /// Indicates if the response is partial and more content is being processed.
    /// </summary>
    [JsonPropertyName("isPartial")]
    public bool IsPartial { get; init; }

    /// <summary>
    /// Optional error message if processing failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>
    /// Error details for debugging if processing failed.
    /// </summary>
    [JsonPropertyName("errorDetails")]
    public Dictionary<string, string>? ErrorDetails { get; init; }

    /// <summary>
    /// Timestamp when this response was generated (UTC).
    /// </summary>
    [JsonPropertyName("responseGeneratedUtc")]
    public DateTime ResponseGeneratedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Processing time in milliseconds for this query.
    /// </summary>
    [JsonPropertyName("processingTimeMs")]
    public long? ProcessingTimeMs { get; init; }

    /// <summary>
    /// Additional response metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; init; } = new();
}