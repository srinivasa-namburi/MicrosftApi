using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

/// <summary>
/// Command to ingest a classic document.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record ClassicDocumentIngestionRequest(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// File name of document to ingest.
    /// </summary>
    public required string FileName { get; set; }
    /// <summary>
    /// Original document URL.
    /// </summary>
    public required string OriginalDocumentUrl { get; set; }
    /// <summary>
    /// OID of the user who uploaded the document.
    /// </summary>
    public string? UploadedByUserOid { get; set; }
    /// <summary>
    /// Document process name.
    /// </summary>
    public required string DocumentProcessName { get; set; }
    /// <summary>
    /// Plugin used for document processing.
    /// </summary>
    public string? Plugin { get; set; }
}
