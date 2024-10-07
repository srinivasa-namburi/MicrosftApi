using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;

public record ContentNodeGenerationStateChanged(Guid CorrelationId)
{
    public Guid ContentNodeId { get; set; }
    public ContentNodeGenerationState GenerationState { get; set; }
}
