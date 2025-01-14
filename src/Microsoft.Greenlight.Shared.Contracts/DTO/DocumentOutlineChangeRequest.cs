namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a request to change the document outline.
/// </summary>
public record DocumentOutlineChangeRequest()
{
    /// <summary>
    /// Unique identifier of the document outline.
    /// </summary>
    public Guid DocumentOutlineId { get; set; }

    /// <summary>
    /// Information about the document outline.
    /// </summary>
    public DocumentOutlineInfo? DocumentOutlineInfo { get; set; }

    /// <summary>
    /// List of changed outline items.
    /// </summary>
    public List<DocumentOutlineItemInfo>? ChangedOutlineItems { get; set; }

    /// <summary>
    /// List of deleted outline items.
    /// </summary>
    public List<DocumentOutlineItemInfo>? DeletedOutlineItems { get; set; }
}
