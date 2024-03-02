using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record IndexIngestedDocument (Guid CorrelationId) : CorrelatedBy<Guid>
{
    
}