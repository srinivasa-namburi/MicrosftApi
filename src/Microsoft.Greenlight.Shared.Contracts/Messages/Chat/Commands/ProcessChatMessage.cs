using Microsoft.Greenlight.Shared.Contracts.Chat;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;

/// <summary>Command to process a chat message.</summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
/// <param name="ChatMessageDto">The chat message to process.</param>
public record ProcessChatMessage(Guid CorrelationId, ChatMessageDTO ChatMessageDto);
