using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;

/// <summary>
/// Event raised when a document outline is generated.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record DocumentOutlineGenerated(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// The generated document in JSON format.
    /// </summary>
    public string GeneratedDocumentJson { get; set; }

    /// <summary>
    /// The OID of the author who generated the document.
    /// </summary>
    public string? AuthorOid { get; set; }
}
