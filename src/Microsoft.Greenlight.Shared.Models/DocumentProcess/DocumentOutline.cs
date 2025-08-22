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
        
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var lines = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var parentStack = new Stack<DocumentOutlineItem>();
        int orderIndex = 0;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }

            // Updated regex to handle various numbering formats and markdown-style headers
            var match = Regex.Match(trimmedLine, @"^((\d+(\.\d+)*\.?)\s+)|(\#+)\s+");
            
            if (match.Success)
            {
                int level;
                string sectionNumber;
                string sectionTitle;

                if (match.Groups[2].Success)
                {
                    // Numbering (1, 1.1, 1.1.1, etc.)
                    var numberPart = match.Groups[2].Value.TrimEnd('.');
                    sectionNumber = numberPart;
                    
                    // Calculate level based on number of dots
                    level = numberPart.Count(c => c == '.');
                    
                    // Extract title (everything after the number and optional dot + space)
                    sectionTitle = trimmedLine.Substring(match.Length).Trim();
                }
                else if (match.Groups[4].Success)
                {
                    // Hashes (#, ##, ###, etc.)
                    var hashPart = match.Groups[4].Value;
                    level = hashPart.Length - 1;
                    sectionNumber = hashPart;
                    
                    // Extract title (everything after the hashes + space)
                    sectionTitle = trimmedLine.Substring(match.Length).Trim();
                }
                else
                {
                    // Fallback - shouldn't happen with current regex, but for safety
                    continue;
                }

                // Create the outline item
                var outlineItem = new DocumentOutlineItem
                {
                    Id = Guid.NewGuid(),
                    Level = level,
                    SectionNumber = sectionNumber,
                    SectionTitle = sectionTitle,
                    OrderIndex = orderIndex++,
                    Children = new List<DocumentOutlineItem>()
                };

                if (level == 0)
                {
                    // Top-level item
                    OutlineItems.Add(outlineItem);
                    parentStack.Clear();
                    parentStack.Push(outlineItem);
                }
                else
                {
                    // Child item - find appropriate parent
                    while (parentStack.Count > 0 && parentStack.Peek().Level >= level)
                    {
                        parentStack.Pop();
                    }

                    if (parentStack.Count > 0)
                    {
                        var parent = parentStack.Peek();
                        outlineItem.ParentId = parent.Id;
                        parent.Children.Add(outlineItem);
                    }
                    else
                    {
                        // No appropriate parent found - treat as top-level
                        OutlineItems.Add(outlineItem);
                    }
                    
                    parentStack.Push(outlineItem);
                }
            }
            else
            {
                // Line doesn't match expected format - could be a simple text line
                // Try to handle it as a simple numbered or bulleted item
                var simpleMatch = Regex.Match(trimmedLine, @"^(\d+\.?\d*\.?\d*)\s+(.+)$");
                if (simpleMatch.Success)
                {
                    var numberPart = simpleMatch.Groups[1].Value.TrimEnd('.');
                    var titlePart = simpleMatch.Groups[2].Value.Trim();
                    
                    var level = numberPart.Count(c => c == '.');
                    
                    var outlineItem = new DocumentOutlineItem
                    {
                        Id = Guid.NewGuid(),
                        Level = level,
                        SectionNumber = numberPart,
                        SectionTitle = titlePart,
                        OrderIndex = orderIndex++,
                        Children = new List<DocumentOutlineItem>()
                    };

                    if (level == 0)
                    {
                        OutlineItems.Add(outlineItem);
                        parentStack.Clear();
                        parentStack.Push(outlineItem);
                    }
                    else
                    {
                        while (parentStack.Count > 0 && parentStack.Peek().Level >= level)
                        {
                            parentStack.Pop();
                        }

                        if (parentStack.Count > 0)
                        {
                            var parent = parentStack.Peek();
                            outlineItem.ParentId = parent.Id;
                            parent.Children.Add(outlineItem);
                        }
                        else
                        {
                            OutlineItems.Add(outlineItem);
                        }
                        
                        parentStack.Push(outlineItem);
                    }
                }
                // If line still doesn't match, skip it
            }
        }

        // No need to sort as we're preserving order from input text
        // Reset order indices for consistent hierarchy
        ResetOrderIndices();
    }

    /// <summary>
    /// Resets the order indices for all outline items to ensure proper ordering.
    /// </summary>
    private void ResetOrderIndices()
    {
        int orderIndex = 0;
        foreach (var item in OutlineItems)
        {
            item.OrderIndex = orderIndex++;
            ResetOrderIndicesRecursive(item);
        }
    }

    /// <summary>
    /// Recursively resets order indices for child items.
    /// </summary>
    /// <param name="item">The parent item.</param>
    private void ResetOrderIndicesRecursive(DocumentOutlineItem item)
    {
        int childOrderIndex = 0;
        foreach (var child in item.Children)
        {
            child.OrderIndex = childOrderIndex++;
            ResetOrderIndicesRecursive(child);
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
