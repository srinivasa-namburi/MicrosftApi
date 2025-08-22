using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

/// <summary>
/// Represents an abstract base class for source reference items.
/// </summary>
public abstract class SourceReferenceItem : EntityBase
{
    /// <summary>
    /// Unique identifier for the content node system item.
    /// </summary>
    public Guid? ContentNodeSystemItemId { get; set; }

    /// <summary>
    /// Content node system item associated with this source reference item.
    /// </summary>
    [JsonIgnore]
    public ContentNodeSystemItem? ContentNodeSystemItem { get; set; }

    /// <summary>
    /// Type of the source reference.
    /// </summary>
    public SourceReferenceType SourceReferenceType { get; set; }

    /// <summary>
    /// Description of the source reference.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Link to the source reference.
    /// </summary>
    public string? SourceReferenceLink { get; set; }

    /// <summary>
    /// Type of the source reference link.
    /// </summary>
    public SourceReferenceLinkType? SourceReferenceLinkType { get; set; }

    /// <summary>
    /// Output of the source reference.
    /// </summary>
    public abstract string? SourceOutput { get; set; }

    /// <summary>
    /// Gets a value indicating whether the source reference link is present.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public bool HasSourceReferenceLink => !string.IsNullOrEmpty(SourceReferenceLink);

    /// <summary>
    /// Gets a value indicating whether the source output is present.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public bool HasSourceOutput => !string.IsNullOrEmpty(SourceOutput);

    /// <summary>
    /// Sets the basic parameters for the source reference item.
    /// </summary>
    public abstract void SetBasicParameters();
}
