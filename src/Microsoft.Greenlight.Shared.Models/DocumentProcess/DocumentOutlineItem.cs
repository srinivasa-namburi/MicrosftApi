using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.DocumentProcess;

/// <summary>
/// Represents an item in the document outline.
/// </summary>
public class DocumentOutlineItem : EntityBase
{
    /// <summary>
    /// Section number of the item.
    /// </summary>
    public string? SectionNumber { get; set; }

    /// <summary>
    /// Section title of the item.
    /// </summary>
    public required string SectionTitle { get; set; }

    /// <summary>
    /// Level of the section of the item.
    /// </summary>
    public required int Level { get; set; } = 0;

    /// <summary>
    /// Additional prompt instructions for this section item which will be pulled from the prompt.
    /// </summary>
    public string? PromptInstructions { get; set; }

    /// <summary>
    /// Value indicating whether this section should not be generated in the final document. 
    /// It will only render as a headline with no content.
    /// </summary>
    public bool RenderTitleOnly { get; set; } = false;

    /// <summary>
    /// Unique identifier of the parent item.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Parent item of the document outline item.
    /// </summary>
    [JsonIgnore]
    public DocumentOutlineItem? Parent { get; set; }

    /// <summary>
    /// Unique identifier of the document outline.
    /// </summary>
    public Guid? DocumentOutlineId { get; set; }

    /// <summary>
    /// Document outline associated with the document outline item.
    /// </summary>
    [JsonIgnore]
    public DocumentOutline? DocumentOutline { get; set; }

    /// <summary>
    /// List of children of the document outline item.
    /// </summary>
    public List<DocumentOutlineItem> Children { get; set; } = [];

    /// <summary>
    /// Order index of the document outline item.
    /// </summary>
    public int? OrderIndex { get; set; } = -1;
}
