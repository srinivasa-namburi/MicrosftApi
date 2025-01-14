using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

/// <summary>
/// Event raised when an ingested document is created in the database.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record IngestedDocumentCreatedInDatabase(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// The ID of the ingested document.
    /// </summary>
    public string FileHash { get; set; }
}
