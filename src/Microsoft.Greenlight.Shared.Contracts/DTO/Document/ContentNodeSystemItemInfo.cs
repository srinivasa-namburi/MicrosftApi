namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents information about a content node system item.
/// </summary>
public class ContentNodeSystemItemInfo
{
    /// <summary>
    /// Unique identifier for the content node system item.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier for the content node.
    /// </summary>
    public Guid ContentNodeId { get; set; }

    /// <summary>
    /// Main prompt instruction.
    /// </summary>
    public string? ComputedUsedMainGenerationPrompt { get; set; }

    /// <summary>
    /// Section prompt instructions.
    /// </summary>
    public string? ComputedSectionPromptInstructions { get; set; }

    /// <summary>
    /// List of source references to generate content.
    /// </summary>
    public List<SourceReferenceItemInfo> SourceReferences { get; set; } = [];
}