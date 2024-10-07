using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;

public record GenerateChatHistorySummary(Guid CorrelationId, DateTime SummaryTime) : CorrelatedBy<Guid>
{
    
}
