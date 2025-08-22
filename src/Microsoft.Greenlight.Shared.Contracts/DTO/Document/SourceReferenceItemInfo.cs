using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents an abstract base class for source reference item information.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$sourceReferenceJsonType")]
[JsonDerivedType(typeof(PluginSourceReferenceItemInfo), nameof(PluginSourceReferenceItemInfo))]
[JsonDerivedType(typeof(KernelMemoryDocumentSourceReferenceItemInfo), nameof(KernelMemoryDocumentSourceReferenceItemInfo))]
[JsonDerivedType(typeof(DocumentLibrarySourceReferenceItemInfo), nameof(DocumentLibrarySourceReferenceItemInfo))]
[JsonDerivedType(typeof(DocumentProcessRepositorySourceReferenceItemInfo), nameof(DocumentProcessRepositorySourceReferenceItemInfo))]
// Vector store aggregated result (Semantic Kernel vector store generic result)
[JsonDerivedType(typeof(VectorStoreSourceReferenceItemInfo), nameof(VectorStoreSourceReferenceItemInfo))]
public abstract class SourceReferenceItemInfo
{
    /// <summary>
    /// Unique identifier for the source reference item.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier for the content node system item.
    /// </summary>
    public Guid ContentNodeSystemItemId { get; set; }

    /// <summary>
    /// Type of the source reference.
    /// </summary>
    public SourceReferenceType SourceReferenceType { get; set; }

    /// <summary>
    /// Description of the source reference item.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Link of the source reference.
    /// </summary>
    public string? SourceReferenceLink { get; set; }

    /// <summary>
    /// Type of the source reference link.
    /// </summary>
    public SourceReferenceLinkType? SourceReferenceLinkType { get; set; }

    /// <summary>
    /// Output of the source reference.
    /// </summary>
    public string? SourceOutput { get; set; }

    /// <summary>
    /// Value indicating whether the source reference link is present.
    /// </summary>
    [JsonIgnore]
    public bool HasSourceReferenceLink => !string.IsNullOrEmpty(SourceReferenceLink);

    /// <summary>
    /// Value indicating whether the source output is present.
    /// </summary>
    [JsonIgnore]
    public bool HasSourceOutput => !string.IsNullOrEmpty(SourceOutput);
}