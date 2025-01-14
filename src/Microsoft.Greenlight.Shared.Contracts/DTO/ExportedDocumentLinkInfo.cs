using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a link to an exported document.
/// </summary>
public record ExportedDocumentLinkInfo
{
    /// <summary>
    /// Unique identifier of the document link.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier of the generated document.
    /// </summary>
    public Guid? GeneratedDocumentId { get; set; }

    /// <summary>
    /// MIME type of the document.
    /// </summary>
    public required string MimeType { get; set; }

    /// <summary>
    /// Type of the file document.
    /// </summary>
    public FileDocumentType Type { get; set; }

    /// <summary>
    /// Absolute URL of the document.
    /// </summary>
    public required string AbsoluteUrl { get; set; }

    /// <summary>
    /// Blob container where the document is stored.
    /// </summary>
    public required string BlobContainer { get; set; }

    /// <summary>
    /// File name of the document.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Date and time when the document link was created.
    /// </summary>
    public DateTimeOffset Created { get; set; }
}
