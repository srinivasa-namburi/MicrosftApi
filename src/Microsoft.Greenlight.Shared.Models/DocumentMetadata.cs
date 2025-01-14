using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents metadata for a document.
/// </summary>
public class DocumentMetadata : EntityBase
{
    /// <summary>
    /// JSON string containing metadata.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// ID of the generated document associated with this metadata.
    /// </summary>
    public Guid GeneratedDocumentId { get; set; }

    /// <summary>
    /// The generated document associated with this metadata.
    /// </summary>
    [JsonIgnore]
    public GeneratedDocument? GeneratedDocument { get; set; }
}

