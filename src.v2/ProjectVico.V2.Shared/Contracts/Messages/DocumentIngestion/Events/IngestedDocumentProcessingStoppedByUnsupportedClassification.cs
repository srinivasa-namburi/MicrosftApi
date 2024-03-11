using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentProcessingStoppedByUnsupportedClassification(Guid CorrelationId) : CorrelatedBy<Guid>;
