using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a content node in the system.
/// </summary>
public class ContentNode : EntityBase
{
    /// <summary>
    /// Text of the content node.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Type of the content node.
    /// </summary>
    public ContentNodeType Type { get; set; }

    /// <summary>
    /// Generation state of the content node.
    /// </summary>
    public ContentNodeGenerationState? GenerationState { get; set; }

    /// <summary>
    /// Children of the content node.
    /// </summary>
    public List<ContentNode> Children { get; set; } = [];

    /// <summary>
    /// Parent of the content node.
    /// </summary>
    [JsonIgnore]
    public ContentNode? Parent { get; set; }

    /// <summary>
    /// Unique parent ID of the content node.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Value indicating whether to render title only.
    /// </summary>
    public bool RenderTitleOnly { get; set; } = false;

    /// <summary>
    /// Prompt instructions for the content node.
    /// </summary>
    public string? PromptInstructions { get; set; }

    /// <summary>
    /// Unique ID for generated document.
    /// This is only used for outer parent nodes to establish the hierarchy.
    /// </summary>
    public Guid? GeneratedDocumentId { get; set; }

    /// <summary>
    /// Generated document associated with the content node.
    /// This is only used for outer parent nodes to establish the hierarchy.
    /// </summary>
    [JsonIgnore]
    public virtual GeneratedDocument? GeneratedDocument { get; set; }

    /// <summary>
    /// Unique ID for the associated generated document.
    /// This is used for inner nodes to establish what document they are associated with. Not to be used
    /// to establish hierarchy of the content nodes.
    /// </summary>
    public Guid? AssociatedGeneratedDocumentId { get; set; }

    /// <summary>
    /// Associated generated document with the content node.
    /// </summary>
    [JsonIgnore]
    public virtual GeneratedDocument? AssociatedGeneratedDocument { get; set; } 
    
    /// <summary>
    /// Unique ID for the content node system item.
    /// </summary>
    public Guid? ContentNodeSystemItemId { get; set; }

    /// <summary>
    /// Content node system item associated with the content node.
    /// </summary>
    [JsonIgnore]
    public virtual ContentNodeSystemItem? ContentNodeSystemItem { get; set; }

    /// <summary>
    /// Unique ID for the version tracker associated with this content node.
    /// </summary>
    public Guid? ContentNodeVersionTrackerId { get; set; }

    /// <summary>
    /// Version tracker associated with the content node.
    /// </summary>
    [JsonIgnore]
    public virtual ContentNodeVersionTracker? ContentNodeVersionTracker { get; set; }
}
