
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public class DocumentIngestionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocumentLibraryShortName { get; set; } = "US.NuclearLicensing";
    public string? Plugin { get; set; }
    public string FileName { get; set; }
    public string OriginalDocumentUrl { get; set; }
    public string? UploadedByUserOid { get; set; }
    public DocumentLibraryType DocumentLibraryType { get; set; } = DocumentLibraryType.PrimaryDocumentProcessLibrary;
}
