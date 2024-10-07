using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentProcessed(Guid CorrelationId) : CorrelatedBy<Guid>
{

};
