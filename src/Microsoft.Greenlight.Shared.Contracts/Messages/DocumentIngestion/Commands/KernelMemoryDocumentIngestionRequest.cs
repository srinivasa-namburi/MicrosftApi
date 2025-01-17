using MassTransit;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

/// <summary>
/// Command to request an ingestion of a document using Kernel Memory.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record KernelMemoryDocumentIngestionRequest(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Name of the file.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// URL of the original document.
    /// </summary>
    public required string OriginalDocumentUrl { get; set; }

    /// <summary>
    /// OID of the user who uploaded the document.
    /// </summary>
    public string? UploadedByUserOid { get; set; }

    /// <summary>
    /// Short name of the document library.
    /// </summary>
    public required string DocumentLibraryShortName { get; set; }

    /// <summary>
    /// Name of the blob container.
    /// </summary>
    public string? BlobContainerName { get; set; }

    /// <summary>
    /// Type of the document library.
    /// </summary>
    public DocumentLibraryType DocumentLibraryType { get; set; } = DocumentLibraryType.PrimaryDocumentProcessLibrary;
}
