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
    /// Original URL of the document.
    /// </summary>
    public required string OriginalDocumentUrl { get; set; }

    /// <summary>
    /// OID of the user who uploaded the document.
    /// </summary>
    public string? UploadedByUserOid { get; set; }

    /// <summary>
    /// Document process associated with the document.
    /// </summary>
    public string? DocumentProcess { get; set; }

    /// <summary>
    /// Plugin used for the document.
    /// </summary>
    public string? Plugin { get; set; }

    /// <summary>
    /// Ingestion state of the document.
    /// </summary>
    public IngestionState IngestionState { get; set; } = IngestionState.Uploaded;

    /// <summary>
    /// Classification short code of the document.
    /// </summary>
    public string? ClassificationShortCode { get; set; }

    /// <summary>
    /// Date when the document was ingested.
    /// </summary>
    public DateTime IngestedDate { get; set; }
}
