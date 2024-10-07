using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentIndexed(Guid CorrelationId) : CorrelatedBy<Guid>;
