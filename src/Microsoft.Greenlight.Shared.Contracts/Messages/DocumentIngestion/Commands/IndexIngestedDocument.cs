using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

/// <summary>
/// Command to index an ingested document.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record IndexIngestedDocument (Guid CorrelationId) : CorrelatedBy<Guid>
{
    
}
