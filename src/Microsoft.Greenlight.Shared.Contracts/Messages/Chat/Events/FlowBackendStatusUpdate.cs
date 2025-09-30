namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;

/// <summary>
/// Event raised when a backend conversation in a Flow orchestration has a status update.
/// Used to route backend status messages through Flow for intelligent synthesis.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
/// <param name="FlowSessionId">The Flow session ID that should receive this status update.</param>
/// <param name="BackendConversationId">The backend conversation ID that generated the status.</param>
/// <param name="MessageId">The specific message ID this status relates to.</param>
/// <param name="StatusMessage">The status message text.</param>
/// <param name="IsProcessingComplete">Whether processing is complete.</param>
/// <param name="IsPersistent">Whether this is a persistent status message.</param>
/// <param name="DocumentProcessName">The document process name for the backend conversation.</param>
/// <param name="Timestamp">When the status was generated.</param>
public record FlowBackendStatusUpdate(
    Guid CorrelationId,
    Guid FlowSessionId,
    Guid BackendConversationId,
    Guid MessageId,
    string StatusMessage,
    bool IsProcessingComplete,
    bool IsPersistent,
    string DocumentProcessName,
    DateTime Timestamp);