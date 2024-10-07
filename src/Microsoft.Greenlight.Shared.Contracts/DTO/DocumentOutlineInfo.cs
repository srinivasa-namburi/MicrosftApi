using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public class DocumentOutlineInfo()
{
    public Guid Id { get; set; }
    public Guid DocumentProcessDefinitionId { get; set; }

    [JsonIgnore]
    public string FullText => RenderOutlineItemsAsText();

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

    private string RenderOutlineItemAsText(DocumentOutlineItemInfo outlineItem, int i)
    {
        var text = "";
        text += new string(' ', i * 2) + outlineItem.SectionNumber + " " + outlineItem.SectionTitle + "\n";
        foreach (var child in outlineItem.Children)
        {
            text += RenderOutlineItemAsText(child, i + 1);
        }

        return text;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not DocumentOutlineInfo other)
            return false;

        return Id == other.Id &&
               DocumentProcessDefinitionId == other.DocumentProcessDefinitionId && 
               OutlineItems.SequenceEqual<DocumentOutlineItemInfo>(other.OutlineItems);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, DocumentProcessDefinitionId);
    }


}
