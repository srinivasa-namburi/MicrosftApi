using Microsoft.Greenlight.Shared.Models.SourceReferences;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// This class holds settings and properties for generated content nodes
/// that aren't necessarily part of the content itself.
/// </summary>
public class ContentNodeSystemItem : EntityBase
{
    public Guid ContentNodeId { get; set; }
    [JsonIgnore]
    public ContentNode? ContentNode { get; set; }

    public string? ComputedUsedMainGenerationPrompt { get; set; }
    public string? ComputedSectionPromptInstructions { get; set; }
    
    public List<SourceReferenceItem> SourceReferences { get; set; } = [];

}