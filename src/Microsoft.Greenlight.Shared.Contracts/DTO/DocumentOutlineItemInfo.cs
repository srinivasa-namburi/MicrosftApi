namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public class DocumentOutlineItemInfo()
{
    public Guid? Id { get; set; }
    public string? SectionNumber { get; set; }
    public required string SectionTitle { get; set; }
    public string? PromptInstructions { get; set; }
    public bool RenderTitleOnly { get; set; }
    public required int Level { get; set; } = 0;
    public Guid? ParentId { get; set; }
    public Guid? DocumentOutlineId { get; set; }
    public List<DocumentOutlineItemInfo> Children { get; set; } = [];
    public int OrderIndex { get; set; } = -1;

    public override bool Equals(object? obj)
    {
        if (obj is not DocumentOutlineItemInfo other)
            return false;

        return Id == other.Id &&
               ParentId == other.ParentId &&
               DocumentOutlineId == other.DocumentOutlineId &&
               Level == other.Level &&
               SectionNumber == other.SectionNumber &&
               SectionTitle == other.SectionTitle &&
               PromptInstructions == other.PromptInstructions &&
               RenderTitleOnly == other.RenderTitleOnly &&
               Children.SequenceEqual(other.Children) &&
               OrderIndex == other.OrderIndex;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, ParentId, DocumentOutlineId, Level, SectionNumber, SectionTitle, PromptInstructions, OrderIndex);
    }
}
