using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;

/// <summary>
/// Event raised when a document is generated.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record GeneratedDocumentCreated(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Metadata ID associated with the generated document.
    /// </summary>
    public required Guid MetaDataId { get; set; }
};
