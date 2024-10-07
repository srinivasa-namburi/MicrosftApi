using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentProcessingStoppedByUnsupportedClassification(Guid CorrelationId) : CorrelatedBy<Guid>;
