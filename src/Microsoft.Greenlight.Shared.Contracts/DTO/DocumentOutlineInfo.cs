using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents the outline information of a document.
/// </summary>
public class DocumentOutlineInfo()
{
    /// <summary>
    /// Unique identifier of the document outline.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier of the document process definition.
    /// </summary>
    public Guid DocumentProcessDefinitionId { get; set; }

    /// <summary>
    /// Gets the full text representation of the document outline.
    /// </summary>
    [JsonIgnore]
    public string FullText => RenderOutlineItemsAsText();

    /// <summary>
    /// List of outline items in the document.
    /// </summary>
    public List<DocumentOutlineItemInfo> OutlineItems { get; set; } = [];

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

    private static string RenderOutlineItemAsText(DocumentOutlineItemInfo outlineItem, int i)
    {
        var text = "";
        text += new string(' ', i * 2) + outlineItem.SectionNumber + " " + outlineItem.SectionTitle + "\n";
        foreach (var child in outlineItem.Children)
        {
            text += RenderOutlineItemAsText(child, i + 1);
        }

        return text;
    }
    /// <summary>
    /// Determines whether the specified object is equal to the other object using Document Outline ID, Document
    /// Process Definition ID, and Outline Items.
    /// </summary>
    /// <param name="obj">The object to compare to the other <see cref="DocumentOutlineInfo"/> object.</param>
    /// <returns>true if the specified object is equal to the other object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not DocumentOutlineInfo other)
            return false;

        return Id == other.Id &&
               DocumentProcessDefinitionId == other.DocumentProcessDefinitionId &&
               OutlineItems.SequenceEqual<DocumentOutlineItemInfo>(other.OutlineItems);
    }

    /// <summary>
    /// Serves as the default hash function using Document Outline ID and Document Process Definition ID.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, DocumentProcessDefinitionId);
    }
}
