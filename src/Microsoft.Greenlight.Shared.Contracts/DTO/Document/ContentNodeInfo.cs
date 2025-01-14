using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents information about a content node.
/// </summary>
public class ContentNodeInfo
{
    /// <summary>
    /// Unique identifier of the content node.
    /// </summary>
    public Guid Id { get; set; }

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
    /// Unique identifier of the parent content node.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Value indicating whether to render the title only.
    /// </summary>
    public bool RenderTitleOnly { get; set; } = false;

    /// <summary>
    /// Prompt instructions for the content node.
    /// </summary>
    public string? PromptInstructions { get; set; }

    /// <summary>
    /// Unique identifier of the content node system item.
    /// </summary>
    public Guid? ContentNodeSystemItemId { get; set; }

    /// <summary>
    /// Content node system item information.
    /// </summary>
    public ContentNodeSystemItemInfo? ContentNodeSystemItem { get; set; }

    /// <summary>
    /// List of child content nodes.
    /// </summary>
    public List<ContentNodeInfo> Children { get; set; } = [];
}