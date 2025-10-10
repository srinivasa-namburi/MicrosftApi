// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts;

/// <summary>
/// Result model for Flow query processing.
/// </summary>
public record FlowQueryResult
{
    /// <summary>
    /// The unified response text.
    /// </summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>
    /// Backend conversation IDs involved in generating this response.
    /// </summary>
    public List<Guid> ConversationIds { get; init; } = new();

    /// <summary>
    /// Processing status.
    /// </summary>
    public string Status { get; init; } = "completed";

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Grain interface for Flow orchestration - manages "super conversations" that can
/// orchestrate multiple document process conversations to provide unified responses.
/// </summary>
public interface IFlowOrchestrationGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Process a query through the Flow orchestration system.
    /// This may create and manage multiple backend conversations based on intent detection.
    /// </summary>
    /// <param name="message">The user's message/query.</param>
    /// <param name="context">Optional context or additional instructions.</param>
    /// <returns>A unified response from potentially multiple conversations.</returns>
    Task<FlowQueryResult> ProcessDocumentQueryAsync(string message, string context);

    /// <summary>
    /// Process a chat message and add it to the user-facing Flow conversation.
    /// This is the new method that integrates with ChatController.
    /// </summary>
    /// <param name="chatMessageDto">The chat message from the user.</param>
    /// <returns>Task for async processing.</returns>
    Task ProcessMessageAsync(ChatMessageDTO chatMessageDto);

    /// <summary>
    /// Get the user-facing conversation messages for display in the UI.
    /// </summary>
    /// <returns>List of chat messages that the user should see.</returns>
    Task<List<ChatMessageDTO>> GetMessagesAsync();

    /// <summary>
    /// Get the current state of the Flow conversation including active backend conversations.
    /// </summary>
    /// <returns>Information about the Flow session state.</returns>
    Task<FlowSessionState> GetStateAsync();

    /// <summary>
    /// Initialize or update the Flow orchestration with user information.
    /// </summary>
    /// <param name="providerSubjectId">The authenticated user's provider subject identifier (from "sub" claim).</param>
    /// <param name="userName">The user's display name.</param>
    Task InitializeAsync(string providerSubjectId, string? userName = null);

    /// <summary>
    /// Engage a specific document process, making its tools/plugins available to Flow.
    /// </summary>
    /// <param name="documentProcessName">The document process to engage.</param>
    /// <returns>List of newly available plugin names.</returns>
    Task<List<string>> EngageDocumentProcessAsync(string documentProcessName);

    /// <summary>
    /// Get all available plugins/tools from engaged document processes.
    /// </summary>
    /// <returns>Dictionary of plugin names and their capabilities.</returns>
    Task<Dictionary<string, object>> GetAvailableToolsAsync();

    /// <summary>
    /// Disengage a document process, removing its tools from Flow.
    /// </summary>
    /// <param name="documentProcessName">The document process to disengage.</param>
    Task DisengageDocumentProcessAsync(string documentProcessName);

    /// <summary>
    /// Start asynchronous processing of a query (non-blocking).
    /// Returns immediately with a task ID for tracking progress.
    /// </summary>
    /// <param name="message">The user's message/query.</param>
    /// <param name="context">Optional context or additional instructions.</param>
    /// <returns>Processing task ID for tracking.</returns>
    Task<string> StartStreamingMessageProcessingAsync(string message, string context);

    /// <summary>
    /// Cancel any active processing and clean up resources.
    /// </summary>
    Task CancelProcessingAsync();

    /// <summary>
    /// Process a query for MCP clients with synchronous-like behavior.
    /// This method waits for all backend conversations to complete before returning.
    /// Unlike ProcessDocumentQueryAsync, this does NOT emit SignalR updates during processing.
    /// MCP clients receive only the final, synthesized response.
    /// </summary>
    /// <param name="message">The user's message/query.</param>
    /// <param name="context">Optional context or additional instructions.</param>
    /// <param name="timeoutSeconds">Maximum seconds to wait for backends to complete (default: 60).</param>
    /// <returns>The final, complete response after all backends have finished.</returns>
    Task<FlowQueryResult> ProcessMessageForMcpAsync(
        string message,
        string context,
        int timeoutSeconds = 60);
}

/// <summary>
/// State information for a Flow orchestration session.
/// </summary>
public record FlowSessionState
{
    /// <summary>
    /// The session identifier (matches the grain key).
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// When this Flow session was created.
    /// </summary>
    public DateTime CreatedUtc { get; init; }

    /// <summary>
    /// Last activity time.
    /// </summary>
    public DateTime LastActivityUtc { get; init; }

    /// <summary>
    /// User who started this Flow session.
    /// </summary>
    public string? UserOid { get; init; }

    /// <summary>
    /// User's display name.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Active backend conversation IDs managed by this Flow orchestration.
    /// </summary>
    public List<Guid> ActiveConversationIds { get; init; } = new();

    /// <summary>
    /// Document processes that have been engaged in this Flow session.
    /// </summary>
    public List<string> EngagedDocumentProcesses { get; init; } = new();

    /// <summary>
    /// Available plugins/tools from engaged document processes.
    /// </summary>
    public List<string> AvailablePlugins { get; init; } = new();

    /// <summary>
    /// Active capabilities provided by engaged document processes.
    /// </summary>
    public List<string> ActiveCapabilities { get; init; } = new();

    /// <summary>
    /// Total number of queries processed in this session.
    /// </summary>
    public int QueryCount { get; init; }

    /// <summary>
    /// Current status of the Flow session.
    /// </summary>
    public FlowSessionStatus Status { get; init; } = FlowSessionStatus.Created;

    /// <summary>
    /// Current response being synthesized (may be partial if still processing).
    /// </summary>
    public string? CurrentResponse { get; init; }
}

/// <summary>
/// Status enumeration for Flow orchestration sessions.
/// </summary>
public enum FlowSessionStatus
{
    /// <summary>
    /// Session has been created but no processing started.
    /// </summary>
    Created,

    /// <summary>
    /// Query is currently being processed across document processes.
    /// </summary>
    Processing,

    /// <summary>
    /// Query processing completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// An error occurred during processing.
    /// </summary>
    Error,

    /// <summary>
    /// Processing was cancelled by user request.
    /// </summary>
    Cancelled
}
