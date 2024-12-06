using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$sourceReferenceJsonType")]
[JsonDerivedType(typeof(PluginSourceReferenceItemInfo), nameof(PluginSourceReferenceItemInfo))]
[JsonDerivedType(typeof(KernelMemoryDocumentSourceReferenceItemInfo), nameof(KernelMemoryDocumentSourceReferenceItemInfo))]
[JsonDerivedType(typeof(DocumentLibrarySourceReferenceItemInfo),nameof(DocumentLibrarySourceReferenceItemInfo))]
[JsonDerivedType(typeof(DocumentProcessRepositorySourceReferenceItemInfo), nameof(DocumentProcessRepositorySourceReferenceItemInfo))]
public abstract class SourceReferenceItemInfo
{
    public Guid Id { get; set; }
    public Guid ContentNodeSystemItemId { get; set; }
    public SourceReferenceType SourceReferenceType { get; set; }
    public string? Description { get; set; }
    public string? SourceReferenceLink { get; set; }
    public SourceReferenceLinkType? SourceReferenceLinkType { get; set; }
    public string? SourceOutput { get; set; }
    [JsonIgnore]
    public bool HasSourceReferenceLink => !string.IsNullOrEmpty(SourceReferenceLink);
    [JsonIgnore]
    public bool HasSourceOutput => !string.IsNullOrEmpty(SourceOutput);

}