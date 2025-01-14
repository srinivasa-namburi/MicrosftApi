using Microsoft.Greenlight.Shared.Models.SourceReferences;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// This class holds settings and properties for generated content nodes
/// that aren't necessarily part of the content itself.
/// </summary>
public class ContentNodeSystemItem : EntityBase
{
    /// <summary>
    /// Unique identifier for the content node.
    /// </summary>
    public Guid ContentNodeId { get; set; }

    /// <summary>
    /// The content node associated with this system item.
    /// </summary>
    [JsonIgnore]
    public ContentNode? ContentNode { get; set; }

    /// <summary>
    /// The computed prompt used for the main generation of the content node.
    /// </summary>
    public string? ComputedUsedMainGenerationPrompt { get; set; }

    /// <summary>
    /// The computed instructions for the section prompt of the content node.
    /// </summary>
    public string? ComputedSectionPromptInstructions { get; set; }

    /// <summary>
    /// List of source references associated with the content node system item.
    /// </summary>
    public List<SourceReferenceItem> SourceReferences { get; set; } = [];
}
