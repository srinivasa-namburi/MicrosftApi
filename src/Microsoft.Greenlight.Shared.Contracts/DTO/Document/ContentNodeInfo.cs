using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

public class ContentNodeInfo
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public ContentNodeType Type { get; set; }
    public ContentNodeGenerationState? GenerationState { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? ContentNodeSystemItemId { get; set; }
    public ContentNodeSystemItemInfo? ContentNodeSystemItem { get; set; }
    public List<ContentNodeInfo> Children { get; set; } = new List<ContentNodeInfo>();
}