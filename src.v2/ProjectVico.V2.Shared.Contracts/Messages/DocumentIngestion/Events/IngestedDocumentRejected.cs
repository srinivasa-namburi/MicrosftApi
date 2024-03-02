using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentRejected(Guid CorrelationId) : CorrelatedBy<Guid>;