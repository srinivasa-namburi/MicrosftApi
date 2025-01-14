using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Chat;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;

/// <summary> Event raised when a chat message response is received.</summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
/// <param name="ChatMessageDto">The chat message received.</param>
/// <param name="LastContentUpdate">The last content update.</param>
public record ChatMessageResponseReceived(Guid CorrelationId, ChatMessageDTO ChatMessageDto, string LastContentUpdate) : CorrelatedBy<Guid>;
