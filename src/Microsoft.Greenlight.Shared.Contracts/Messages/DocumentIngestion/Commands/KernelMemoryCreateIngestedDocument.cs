using MassTransit;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record KernelMemoryCreateIngestedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string FileName { get; set; }
    public string OriginalDocumentUrl { get; set; }
    public string? UploadedByUserOid { get; set; }
    public string? DocumentLibraryShortName { get; set; }
    public DocumentLibraryType DocumentLibraryType { get; set; } = DocumentLibraryType.PrimaryDocumentProcessLibrary;
}
