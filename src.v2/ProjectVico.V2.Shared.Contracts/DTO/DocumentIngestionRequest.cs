using ProjectVico.V2.Shared.Models.Enums;

namespace ProjectVico.V2.Shared.Contracts.DTO;

public class DocumentIngestionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; }
    public string OriginalDocumentUrl { get; set; }
    public string? UploadedByUserOid { get; set; }
    public IngestionType IngestionType { get; set; }
}