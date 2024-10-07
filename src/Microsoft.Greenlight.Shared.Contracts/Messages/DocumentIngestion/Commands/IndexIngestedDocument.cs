using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record IndexIngestedDocument (Guid CorrelationId) : CorrelatedBy<Guid>
{
    
}
