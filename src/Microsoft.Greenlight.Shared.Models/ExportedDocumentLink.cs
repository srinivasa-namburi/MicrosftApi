using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a link to an exported document in the system.
/// </summary>
public class ExportedDocumentLink : EntityBase
{
    /// <summary>
    /// Unique ID of the generated document.
    /// </summary>
    public Guid? GeneratedDocumentId { get; set; }

    /// <summary>
    /// Generated document associated with the exported document link.
    /// </summary>
    [JsonIgnore]
    public virtual GeneratedDocument? GeneratedDocument { get; set; }

    /// <summary>
    /// MIME type of the document.
    /// </summary>
    public required string MimeType { get; set; }

    /// <summary>
    /// File type of the document.
    /// </summary>
    public FileDocumentType Type { get; set; }

    /// <summary>
    /// Absolute URL of the document.
    /// </summary>
    public required string AbsoluteUrl { get; set; }

    /// <summary>
    /// Name of the blob container where the document is stored.
    /// </summary>
    public required string BlobContainer { get; set; }

    /// <summary>
    /// File name of the document.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Date and time when the document was created.
    /// </summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// Hash of the file content for deduplication.
    /// </summary>
    public string? FileHash { get; set; }
}
