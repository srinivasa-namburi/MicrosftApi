using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Chat;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;

public record ProcessChatMessage(Guid CorrelationId, ChatMessageDTO ChatMessageDto) : CorrelatedBy<Guid>;
