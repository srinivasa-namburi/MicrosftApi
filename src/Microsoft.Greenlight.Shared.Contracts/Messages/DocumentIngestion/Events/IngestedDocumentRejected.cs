using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentRejected(Guid CorrelationId) : CorrelatedBy<Guid>;
