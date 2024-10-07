using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentClassificationFailed(Guid CorrelationId) : CorrelatedBy<Guid>
{

}
