using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentIndexed(Guid CorrelationId) : CorrelatedBy<Guid>;