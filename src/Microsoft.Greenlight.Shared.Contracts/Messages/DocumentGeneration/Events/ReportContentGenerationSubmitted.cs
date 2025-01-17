using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;

/// <summary>
/// Event raised when a report content generation is submitted.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record ReportContentGenerationSubmitted(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Number of content nodes use for generation.
    /// </summary>
    public int NumberOfContentNodesToGenerate { get; set; }

    /// <summary>
    /// Author OID.
    /// </summary>
    public string? AuthorOid { get; set; }
}
