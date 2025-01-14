namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents an item in the document outline.
/// </summary>
public class DocumentOutlineItemInfo()
{
    /// <summary>
    /// Unique identifier of the item.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Section number of the item.
    /// </summary>
    public string? SectionNumber { get; set; }

    /// <summary>
    /// Title of the section.
    /// </summary>
    public required string SectionTitle { get; set; }

    /// <summary>
    /// Prompt instructions for the item.
    /// </summary>
    public string? PromptInstructions { get; set; }

    /// <summary>
    /// Value indicating whether to render the title only.
    /// </summary>
    public bool RenderTitleOnly { get; set; }

    /// <summary>
    /// Level of the item in the outline.
    /// </summary>
    public required int Level { get; set; } = 0;

    /// <summary>
    /// Unique identifier of the parent item.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Unique identifier of the document outline.
    /// </summary>
    public Guid? DocumentOutlineId { get; set; }

    /// <summary>
    /// Child items of this item.
    /// </summary>
    public List<DocumentOutlineItemInfo> Children { get; set; } = [];

    /// <summary>
    /// Order index of the item.
    /// </summary>
    public int OrderIndex { get; set; } = -1;

    /// <summary>
    /// Determines whether the specified object is equal to the other <see cref="DocumentOutlineItemInfo"/> object by comparing
    /// all properties of <see cref="DocumentOutlineItemInfo"/>.
    /// </summary>
    /// <param name="obj">The object to compare with the other <see cref="DocumentOutlineItemInfo"/> object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
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

    /// <summary>
    /// Serves as the default hash function using properties of <see cref="DocumentOutlineItemInfo"/>.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, ParentId, DocumentOutlineId, Level, SectionNumber, SectionTitle, PromptInstructions, OrderIndex);
    }
}
