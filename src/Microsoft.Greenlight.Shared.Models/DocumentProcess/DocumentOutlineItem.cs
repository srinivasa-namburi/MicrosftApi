using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.DocumentProcess;

public class DocumentOutlineItem : EntityBase
{
    public string? SectionNumber { get; set; }
    public required string SectionTitle { get; set; }
    public required int Level { get; set; } = 0;
    /// <summary>
    /// Additional Prompt Instructions for this section item which will be pulled from the prompt.
    /// </summary>
    public string? PromptInstructions { get; set; }

    /// <summary>
    /// Flag to indicate that this section should not be generated in the final document. It will only render as a headline with no content.
    /// </summary>
    public bool RenderTitleOnly { get; set; } = false;
    public Guid? ParentId { get; set; }
    [JsonIgnore]
    public DocumentOutlineItem? Parent { get; set; }
    public Guid? DocumentOutlineId { get; set; }
    [JsonIgnore]
    public DocumentOutline? DocumentOutline { get; set; }
    public List<DocumentOutlineItem> Children { get; set; } = [];
    public int? OrderIndex { get; set; } = -1;
}