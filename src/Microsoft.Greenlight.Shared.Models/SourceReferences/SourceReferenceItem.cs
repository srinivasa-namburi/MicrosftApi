using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

public abstract class SourceReferenceItem : EntityBase
{
    public Guid ContentNodeSystemItemId { get; set; }
    [JsonIgnore]
    public ContentNodeSystemItem? ContentNodeSystemItem { get; set; }
    public SourceReferenceType SourceReferenceType { get; set; }
    public string? Description { get; set; }
    public string? SourceReferenceLink { get; set; }
    public SourceReferenceLinkType? SourceReferenceLinkType { get; set; }
    public abstract string? SourceOutput { get; set; }
    [NotMapped]
    [JsonIgnore]
    public bool HasSourceReferenceLink => !string.IsNullOrEmpty(SourceReferenceLink);
    [NotMapped]
    [JsonIgnore]
    public bool HasSourceOutput => !string.IsNullOrEmpty(SourceOutput);
    
    public abstract void SetBasicParameters();
}