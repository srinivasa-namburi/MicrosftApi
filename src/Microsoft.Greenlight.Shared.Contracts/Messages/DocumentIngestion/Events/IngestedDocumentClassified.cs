using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

/// <summary>
/// Event raised when a document is classified.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record IngestedDocumentClassified(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Short code of the classification.
    /// </summary>
    public required string ClassificationShortCode { get; set; }

}
