namespace ProjectVico.V2.Shared.Contracts.DTO;

public record DocumentOutlineChangeRequest()
{
    public Guid DocumentOutlineId { get; set; }
    public DocumentOutlineInfo? DocumentOutlineInfo { get; set; }
    public List<DocumentOutlineItemInfo>? ChangedOutlineItems { get; set; }
    public List<DocumentOutlineItemInfo>? DeletedOutlineItems { get; set; }
}