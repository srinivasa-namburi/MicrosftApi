using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public record ExportedDocumentLinkInfo
{
    public Guid Id { get; set; }
    public Guid? GeneratedDocumentId { get; set; }
    
    public string MimeType { get; set; }

    public FileDocumentType Type { get; set; }

    public string AbsoluteUrl { get; set; }

    public string BlobContainer { get; set; }

    public string FileName { get; set; }

    public DateTimeOffset Created { get; set; }
}
