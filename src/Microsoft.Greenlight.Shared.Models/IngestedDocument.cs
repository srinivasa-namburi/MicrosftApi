using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a document that has been ingested into the system.
/// </summary>
public class IngestedDocument : EntityBase
{
    /// <summary>
    /// File name of the ingested document.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// File hash of the ingested document.
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// The original blob URL of the file before it is copied.
    /// </summary>
    public required string OriginalDocumentUrl { get; set; }

    /// <summary>
    /// The final blob URL of the file after it has been copied to its destination.
    /// </summary>
    public string? FinalBlobUrl { get; set; }

    /// <summary>
    /// OID of the user who uploaded the document.
    /// </summary>
    public string? UploadedByUserOid { get; set; }

    /// <summary>
    /// Name of the document library or process associated with the document.
    /// </summary>
    public string? DocumentLibraryOrProcessName { get; set; }

    /// <summary>
    /// The type of document ingestion (library or process).
    /// </summary>
    public DocumentLibraryType DocumentLibraryType { get; set; }

    /// <summary>
    /// Ingestion state of the document.
    /// </summary>
    public IngestionState IngestionState { get; set; } = IngestionState.Discovered;

    /// <summary>
    /// Date when the document was ingested.
    /// </summary>
    public DateTime IngestedDate { get; set; }

    /// <summary>
    /// The container in which the file resides.
    /// </summary>
    public required string Container { get; set; }

    /// <summary>
    /// The folder path within the container.
    /// </summary>
    public required string FolderPath { get; set; }

    /// <summary>
    /// The orchestration ID (SHA256 of container + folder) for this ingestion batch.
    /// </summary>
    public required string OrchestrationId { get; set; }

    /// <summary>
    /// The RunId for the ingestion batch this document belongs to.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// Error message if ingestion failed.
    /// </summary>
    public string? Error { get; set; }
}
