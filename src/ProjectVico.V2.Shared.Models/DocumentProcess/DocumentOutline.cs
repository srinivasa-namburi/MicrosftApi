using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ProjectVico.V2.Shared.Models.DocumentProcess;

public class DocumentOutline : EntityBase
{
    /// <summary>
    /// Contains the full text of the outline. Each section on a new line.
    /// Use either numbering (1, 1.1, 1.1.1, etc.) or hashes (#, ##, ###, etc.) to indicate hierarchy.
    /// Each line must begin with the section number or a set of asterisks, followed by a space, and then the section title.
    /// </summary>

    public string FullText
    {
        get => RenderOutlineItemsAsText();
        set => ParseOutlineItemsFromText(value);
    }

    public Guid DocumentProcessDefinitionId { get; set; }
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DocumentProcessDefinition { get; set; }

    /// <summary>
    /// The OutlineItems are Auto Included in the Context
    /// </summary>
    public virtual List<DocumentOutlineItem> OutlineItems { get; set; }

    public bool TextUsesNumbers()
    {
        // Determine if the outline uses numbers or hashes to indicate hierarchy
        var sampleOutlineItem = OutlineItems.FirstOrDefault(x => x.Level == 0);
        return !string.IsNullOrEmpty(sampleOutlineItem!.SectionNumber);
    }

    private void ParseOutlineItemsFromText(string value)
    {
        // Parse the full text into outline items
        // Each line in the full text represents a section in the outline
        // The section number is the first part of the line, followed by a space, and then the section title
        // The section number can be a number (1, 1.1, 1.1.1, etc.) or a set of hashes (#, ##, ###, etc.)
        // The number of hashes indicates the level of the section
        // Use the Level property to store the level of the section
        // Use the SectionNumber and SectionTitle properties to store the section number and title
        // Use the ParentId property to store the id of the parent section
        // Use the Children property to store the child sections

        OutlineItems = [];
        var lines = value.Split('\n');
        var parentStack = new Stack<DocumentOutlineItem>();
        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^(\d+\.)*\d+|#* ");
            if (match.Success)
            {
                var level = match.Groups[1].Captures.Count;
                var sectionNumber = match.Value.Trim();
                var sectionTitle = line.Substring(match.Length).Trim();
                var outlineItem = new DocumentOutlineItem
                {
                    Level = level,
                    SectionNumber = sectionNumber,
                    SectionTitle = sectionTitle
                };

                if (level == 0)
                {
                    OutlineItems.Add(outlineItem);
                    parentStack.Clear();
                    parentStack.Push(outlineItem);
                }
                else
                {
                    while (parentStack.Count > level)
                    {
                        parentStack.Pop();
                    }

                    var parent = parentStack.Peek();
                    outlineItem.Parent = parent;
                    parent.Children.Add(outlineItem);
                    parentStack.Push(outlineItem);
                }
            }
        }

    }

    private string RenderOutlineItemsAsText()
    {
        // traverse the outline items and render them as text
        // for each level, indent the text by 2 spaces (the initial/outer level is level 0)
        // Use the Level property to determine the indentation level
        // Use the SectionNumber and SectionTitle properties to render the text
        // Use the Children property to traverse the children of each outline item

        var text = "";
        foreach (var outlineItem in OutlineItems.Where(x => x.Level == 0))
        {
            text += RenderOutlineItemAsText(outlineItem, 0);
        }

        return text;
    }

    private string RenderOutlineItemAsText(DocumentOutlineItem outlineItem, int i)
    {
        var text = "";
        text += new string(' ', i * 2) + outlineItem.SectionNumber + " " + outlineItem.SectionTitle + "\n";
        foreach (var child in outlineItem.Children)
        {
            text += RenderOutlineItemAsText(child, i + 1);
        }
        return text;

    }
}

public class DocumentOutlineItem : EntityBase
{
    public string? SectionNumber { get; set; }
    public required string SectionTitle { get; set; }
    public required int Level { get; set; } = 0;
    /// <summary>
    /// Additional Prompt Instructions for this section item which will be pulled from the prompt.
    /// </summary>
    public string? PromptInstructions { get; set; }
    public Guid? ParentId { get; set; }
    [JsonIgnore]
    public DocumentOutlineItem? Parent { get; set; }
    public Guid? DocumentOutlineId { get; set; }
    [JsonIgnore]
    public DocumentOutline? DocumentOutline { get; set; }
    public List<DocumentOutlineItem> Children { get; set; } = [];
    public int? OrderIndex { get; set; } = -1;
}

