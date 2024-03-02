using System.Text.Json.Serialization;
using ProjectVico.V2.Shared.Models.Enums;

namespace ProjectVico.V2.Shared.Models;

public class ContentNode : EntityBase
{
    public string Text { get; set; } = string.Empty;
    public ContentNodeType Type { get; set; }
    public ContentNodeGenerationState? GenerationState { get; set; }
    public List<ContentNode> Children { get; set; } = new List<ContentNode>();
    [JsonIgnore]
    public ContentNode? Parent { get; set; }
    public Guid? ParentId { get; set; }

    public Guid? IngestedDocumentId { get; set; }
    public virtual IngestedDocument? IngestedDocument { get; set; }
    
    public Guid? GeneratedDocumentId { get; set; }
    public virtual GeneratedDocument? GeneratedDocument { get; set; }

    public List<BoundingRegion>? BoundingRegions { get; set; } = new List<BoundingRegion>();


}