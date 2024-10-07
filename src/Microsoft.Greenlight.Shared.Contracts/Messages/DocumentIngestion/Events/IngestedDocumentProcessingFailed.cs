using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentProcessingFailed(Guid CorrelationId) : CorrelatedBy<Guid>
{

}
