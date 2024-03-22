using MassTransit;
using ProjectVico.V2.Shared.Contracts.Chat;

namespace ProjectVico.V2.Shared.Contracts.Messages.Chat.Commands;

public record ProcessChatMessage(Guid CorrelationId, ChatMessageDTO ChatMessageDto) : CorrelatedBy<Guid>;