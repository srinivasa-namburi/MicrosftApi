using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Chat;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;

public record ChatMessageResponseReceived(Guid CorrelationId, ChatMessageDTO ChatMessageDto, string LastContentUpdate) : CorrelatedBy<Guid>;
