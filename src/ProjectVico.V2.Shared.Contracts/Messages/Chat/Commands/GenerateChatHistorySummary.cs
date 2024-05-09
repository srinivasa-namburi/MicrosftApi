using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.Chat.Commands;

public record GenerateChatHistorySummary(Guid CorrelationId, DateTime SummaryTime) : CorrelatedBy<Guid>
{
    
}