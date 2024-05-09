using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;

public record ContentNodeGenerationStateChanged(Guid CorrelationId)
{
    public Guid ContentNodeId { get; set; }
    public ContentNodeGenerationState GenerationState { get; set; }
}