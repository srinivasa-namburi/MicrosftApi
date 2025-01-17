using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

/// <summary>
/// Command to process an ingested document.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record ProcessIngestedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
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
    /// Name of the document process.
    /// </summary>
    public string? DocumentProcessName { get; set; }

    /// <summary>
    /// Plugin used for processing the document.
    /// </summary>
    public string? Plugin { get; set; }
}
