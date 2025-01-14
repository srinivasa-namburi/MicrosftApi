using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a request for document ingestion.
/// </summary>
public class DocumentIngestionRequest
{
    /// <summary>
    /// Unique identifier for the document ingestion request.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Short name of the document library.
    /// </summary>
    public string DocumentLibraryShortName { get; set; } = "US.NuclearLicensing";

    /// <summary>
    /// Plugin associated with the document ingestion request.
    /// </summary>
    public string? Plugin { get; set; }

    /// <summary>
    /// File name of the document.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Original URL of the document.
    /// </summary>
    public string OriginalDocumentUrl { get; set; }

    /// <summary>
    /// OID of the user who uploaded the document.
    /// </summary>
    public string? UploadedByUserOid { get; set; }

    /// <summary>
    /// Type of the document library.
    /// </summary>
    public DocumentLibraryType DocumentLibraryType { get; set; } = DocumentLibraryType.PrimaryDocumentProcessLibrary;
}
