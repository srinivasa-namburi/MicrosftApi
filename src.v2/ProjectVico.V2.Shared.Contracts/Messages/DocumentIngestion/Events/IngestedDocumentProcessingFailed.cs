using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentProcessingFailed(Guid CorrelationId) : CorrelatedBy<Guid>
{

}