using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record ReindexAllCompletedDocuments(Guid CorrelationId):CorrelatedBy<Guid>;