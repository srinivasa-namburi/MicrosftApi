using MassTransit;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

/// <summary>
/// Command to classify an ingested document.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record ClassifyIngestedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// The name of the document process.
    /// </summary>
    public required string DocumentProcessName { get; set; }

    /// <summary>
    /// The name of the file.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// The original URL of the document.
    /// </summary>
    public required string OriginalDocumentUrl { get; set; }

    /// <summary>
    /// The OID of the user who uploaded the document.
    /// </summary>
    public string? UploadedByUserOid { get; set; }

    /// <summary>
    /// The type of ingestion.
    /// </summary>
    public IngestionType IngestionType { get; set; }

    /// <summary>
    /// The plugin used for ingestion.
    /// </summary>
    public string? Plugin { get; set; }
}
