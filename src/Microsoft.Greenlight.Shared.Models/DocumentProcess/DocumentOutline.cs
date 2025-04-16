using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Shared.Models.DocumentProcess;

/// <summary>
/// Represents a document outline.
/// </summary>
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

    /// <summary>
    /// Unique identifier of the document process definition.
    /// </summary>
    public Guid DocumentProcessDefinitionId { get; set; }

    /// <summary>
    /// Dynamic document process definition associated with this document outline.
    /// </summary>
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DocumentProcessDefinition { get; set; }

    /// <summary>
    /// The OutlineItems are Auto Included in the Context
    /// </summary>
    public virtual List<DocumentOutlineItem> OutlineItems { get; set; } = null!;

    /// <summary>
    /// Determines if the outline uses numbers or hashes to indicate hierarchy.
    /// </summary>
    /// <returns>True if the outline uses numbers, otherwise false.</returns>
    public bool TextUsesNumbers()
    {
        var sampleOutlineItem = OutlineItems.FirstOrDefault(x => x.Level == 0);
        return !string.IsNullOrEmpty(sampleOutlineItem!.SectionNumber);
    }

    private void ParseOutlineItemsFromText(string value)
    {
        // Parse the full text into outline items
        OutlineItems = new List<DocumentOutlineItem>();
        var lines = value.Split('\n');
        var parentStack = new Stack<DocumentOutlineItem>();
        int orderIndex = 0;

        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^(\d+(\.\d+)*\.?)|(\#+) ");
            if (match.Success)
            {
                int level;
                string sectionNumber;
                if (match.Groups[1].Success)
                {
                    // Numbering (1, 1.1, 1.1.1, etc.)
                    level = match.Groups[1].Value.Count(c => c == '.');
                    sectionNumber = match.Groups[1].Value.TrimEnd('.'); // Strip trailing periods if present
                }
                else
                {
                    // Hashes (#, ##, ###, etc.)
                    level = match.Groups[3].Value.Length - 1;
                    sectionNumber = match.Groups[3].Value; // Store the hashes as the section number
                }

                // Adjust sectionTitle to exclude any leading space or period
                var sectionTitle = line.Substring(match.Length).TrimStart(' ', '.').Trim();

                var outlineItem = new DocumentOutlineItem
                {
                    Level = level,
                    SectionNumber = sectionNumber,
                    SectionTitle = sectionTitle,
                    OrderIndex = orderIndex++ // Preserve the order from the input text
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
                    outlineItem.ParentId = parent.Id;
                    parent.Children.Add(outlineItem);
                    parentStack.Push(outlineItem);
                }
            }
        }

        // Sort the outline items by Level and OrderIndex
        OutlineItems = OutlineItems
            .OrderBy(item => item.Level)
            .ThenBy(item => item.SectionNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.OrderIndex)
            .ToList();
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

    /// <summary>
    /// Renders a single outline item as text.
    /// </summary>
    /// <param name="outlineItem">The outline item to render.</param>
    /// <param name="indentLevel">The indentation level.</param>
    /// <returns>The text representation of the outline item.</returns>
    private string RenderOutlineItemAsText(DocumentOutlineItem outlineItem, int indentLevel)
    {
        var text = "";
        text += new string(' ', indentLevel * 2) + outlineItem.SectionNumber + " " + outlineItem.SectionTitle + "\n";
        foreach (var child in outlineItem.Children)
        {
            text += RenderOutlineItemAsText(child, indentLevel + 1);
        }
        return text;
    }
}
