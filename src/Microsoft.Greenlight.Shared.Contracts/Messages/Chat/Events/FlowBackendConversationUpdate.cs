using Microsoft.Greenlight.Shared.Contracts.Chat;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;

/// <summary>
/// Event raised when a Flow backend conversation receives an update.
/// Used for real-time monitoring of backend conversations by Flow orchestration grains.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
/// <param name="FlowSessionId">The Flow session ID that should receive this update.</param>
/// <param name="BackendConversationId">The backend conversation ID that was updated.</param>
/// <param name="ChatMessageDto">The chat message that was added to the backend conversation.</param>
/// <param name="DocumentProcessName">The document process name for the backend conversation.</param>
/// <param name="IsComplete">Whether the backend conversation response is complete.</param>
public record FlowBackendConversationUpdate(
    Guid CorrelationId,
    Guid FlowSessionId,
    Guid BackendConversationId,
    ChatMessageDTO ChatMessageDto,
    string DocumentProcessName,
    bool IsComplete);