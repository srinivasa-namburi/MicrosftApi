using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record ReindexAllCompletedDocuments(Guid CorrelationId):CorrelatedBy<Guid>;
