using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

/// <summary>
/// Command to reindex all completed documents.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record ReindexAllCompletedDocuments(Guid CorrelationId):CorrelatedBy<Guid>;
