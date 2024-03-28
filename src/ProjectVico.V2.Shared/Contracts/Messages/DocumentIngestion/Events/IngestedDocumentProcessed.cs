using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentProcessed(Guid CorrelationId) : CorrelatedBy<Guid>
{

};