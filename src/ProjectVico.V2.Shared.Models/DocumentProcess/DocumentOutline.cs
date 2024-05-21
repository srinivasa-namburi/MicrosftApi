using System.Text.Json.Serialization;

namespace ProjectVico.V2.Shared.Models.DocumentProcess;

public class DocumentOutline : EntityBase
{
    /// <summary>
    /// Contains the full text of the outline. Each section on a new line.
    /// Use either numbering (1, 1.1, 1.1.1, etc.) or hashes (#, ##, ###, etc.) to indicate hierarchy.
    /// Each line must begin with the section number or a set of asterisks, followed by a space, and then the section title.
    /// </summary>
    public string FullText { get; set; }

    public Guid DocumentProcessDefinitionId { get; set; }
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DocumentProcessDefinition { get; set; }

    public bool TextUsesNumbers()
    {
        // Determine if the outline uses numbers or hashes to indicate hierarchy
        return FullText.Contains("1.") || FullText.Contains("1.1.") || FullText.Contains("1.1.1.");
    }

    
   
}

public class DocumentOutlineItem
{
    public string SectionNumber { get; set; }
    public string SectionTitle { get; set; }
    public string? ParentSectionNumber { get; set; }
    public int Level { get; set; }
    public List<DocumentOutlineItem> Children { get; set; } = [];
}

