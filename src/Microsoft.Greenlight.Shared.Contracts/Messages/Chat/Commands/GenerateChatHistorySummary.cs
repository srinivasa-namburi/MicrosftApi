using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Commands;

/// <summary>Command to generate a summary of chat history.</summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
/// <param name="SummaryTime">The time at which to generate the summary.</param>
public record GenerateChatHistorySummary(Guid CorrelationId, DateTime SummaryTime) : CorrelatedBy<Guid>
{
    
}
