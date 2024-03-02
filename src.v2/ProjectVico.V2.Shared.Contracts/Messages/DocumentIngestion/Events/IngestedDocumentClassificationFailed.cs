using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentClassificationFailed(Guid CorrelationId) : CorrelatedBy<Guid>
{

}