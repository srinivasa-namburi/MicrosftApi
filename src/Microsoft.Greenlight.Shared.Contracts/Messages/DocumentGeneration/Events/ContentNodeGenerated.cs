using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;

/// <summary>
/// Event raised when a content node is generated.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record ContentNodeGenerated(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Content node ID.
    /// </summary>
    public Guid ContentNodeId { get; set; }

    /// <summary>
    /// Value indicating whether the generation was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Author OID.
    /// </summary>
    public string AuthorOid { get; set; }
}
