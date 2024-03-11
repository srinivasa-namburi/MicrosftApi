using System.Text.Json.Serialization;

namespace ProjectVico.V2.Shared.Models;

public class DocumentMetadata : EntityBase
{
    public string? MetadataJson { get; set; }

    public Guid GeneratedDocumentId { get; set; }
    [JsonIgnore]
    public GeneratedDocument GeneratedDocument { get; set; }
}