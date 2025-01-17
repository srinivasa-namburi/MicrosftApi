using MassTransit;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

/// <summary>
/// Command to ingest documents from an auto import path.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record IngestDocumentsFromAutoImportPath(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Name of the blob container.
    /// </summary>
    public required string BlobContainerName { get; set; }

    /// <summary>
    /// Folder path in blob container.
    /// </summary>
    public required string FolderPath { get; set; }

    /// <summary>
    /// Short name of the document library.
    /// </summary>
    public string? DocumentLibraryShortName { get; set; }

    /// <summary>
    /// Type of the document library.
    /// </summary>
    public DocumentLibraryType DocumentLibraryType { get; set; } = DocumentLibraryType.PrimaryDocumentProcessLibrary;
}
