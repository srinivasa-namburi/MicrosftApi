using MassTransit;
using ProjectVico.V2.Shared.Contracts.Chat;

namespace ProjectVico.V2.Shared.Contracts.Messages.Chat.Events;

public record ChatMessageResponseReceived(Guid CorrelationId, ChatMessageDTO ChatMessageDto, string LastContentUpdate) : CorrelatedBy<Guid>;