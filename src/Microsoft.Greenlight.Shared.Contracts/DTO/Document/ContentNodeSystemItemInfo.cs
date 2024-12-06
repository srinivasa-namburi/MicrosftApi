namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

public class ContentNodeSystemItemInfo
{
    public Guid Id { get; set; }
    public Guid ContentNodeId { get; set; }
    public string? ComputedUsedMainGenerationPrompt { get; set; }
    public string? ComputedSectionPromptInstructions { get; set; }
    public List<SourceReferenceItemInfo> SourceReferences { get; set; } = new List<SourceReferenceItemInfo>();
}