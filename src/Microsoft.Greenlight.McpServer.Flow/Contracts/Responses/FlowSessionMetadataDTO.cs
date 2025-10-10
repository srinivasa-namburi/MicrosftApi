// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.McpServer.Flow.Contracts.Responses;

/// <summary>
/// Provides comprehensive metadata about the current Flow session state.
/// This information helps MCP clients understand the context and capabilities of the conversation.
/// </summary>
public record FlowSessionMetadataDTO
{
    /// <summary>
    /// The Flow session identifier.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this Flow session was created.
    /// </summary>
    [JsonPropertyName("createdUtc")]
    public DateTime CreatedUtc { get; init; }

    /// <summary>
    /// UTC timestamp of the last activity in this session.
    /// </summary>
    [JsonPropertyName("lastActivityUtc")]
    public DateTime LastActivityUtc { get; init; }

    /// <summary>
    /// Total number of queries processed in this session.
    /// </summary>
    [JsonPropertyName("queryCount")]
    public int QueryCount { get; init; }

    /// <summary>
    /// Total number of messages in the conversation history.
    /// </summary>
    [JsonPropertyName("messageCount")]
    public int MessageCount { get; init; }

    /// <summary>
    /// User information for this session.
    /// </summary>
    [JsonPropertyName("userInfo")]
    public FlowUserInfoDTO? UserInfo { get; init; }

    /// <summary>
    /// Document processes currently engaged in this Flow session.
    /// These processes are actively contributing to responses.
    /// </summary>
    [JsonPropertyName("engagedDocumentProcesses")]
    public List<string> EngagedDocumentProcesses { get; init; } = new();

    /// <summary>
    /// Backend conversation IDs being orchestrated by Flow.
    /// For debugging and tracing purposes.
    /// </summary>
    [JsonPropertyName("activeBackendConversations")]
    public List<string> ActiveBackendConversations { get; init; } = new();

    /// <summary>
    /// Available plugins/tools from engaged document processes.
    /// </summary>
    [JsonPropertyName("availablePlugins")]
    public List<string> AvailablePlugins { get; init; } = new();

    /// <summary>
    /// Active capabilities provided by engaged document processes.
    /// </summary>
    [JsonPropertyName("activeCapabilities")]
    public List<string> ActiveCapabilities { get; init; } = new();

    /// <summary>
    /// Current session status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "active";

    /// <summary>
    /// Session expiration time (UTC).
    /// After this time, the session may be cleaned up.
    /// </summary>
    [JsonPropertyName("expiresUtc")]
    public DateTime? ExpiresUtc { get; init; }

    /// <summary>
    /// Additional session metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// User information associated with a Flow session.
/// </summary>
public record FlowUserInfoDTO
{
    /// <summary>
    /// User's object identifier.
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// User's display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// User's email address if available.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }
}