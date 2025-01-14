using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;

/// <summary>
/// Event raised when the state of a content node generation changes.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record ContentNodeGenerationStateChanged(Guid CorrelationId)
{
    /// <summary>
    /// Content node ID.
    /// </summary>
    public Guid ContentNodeId { get; set; }

    /// <summary>
    /// Generation state of the content node.
    /// </summary>
    public ContentNodeGenerationState GenerationState { get; set; }
}
