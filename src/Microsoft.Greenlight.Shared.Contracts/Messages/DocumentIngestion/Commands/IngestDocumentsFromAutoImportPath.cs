using MassTransit;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record IngestDocumentsFromAutoImportPath(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string BlobContainerName { get; set; }
    public string FolderPath { get; set; }
    public string? DocumentLibraryShortName { get; set; }
    public DocumentLibraryType DocumentLibraryType { get; set; } = DocumentLibraryType.PrimaryDocumentProcessLibrary;
}
