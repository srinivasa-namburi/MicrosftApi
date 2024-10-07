using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models;

public class ExportedDocumentLink : EntityBase
{
    public Guid? GeneratedDocumentId { get; set; }
    [JsonIgnore]
    public virtual GeneratedDocument? GeneratedDocument { get; set; }

    public string MimeType { get; set; }

    public FileDocumentType Type { get; set; }

    public string AbsoluteUrl { get; set; }

    public string BlobContainer { get; set; }

    public string FileName { get; set; }

    public DateTimeOffset Created { get; set; }
}
