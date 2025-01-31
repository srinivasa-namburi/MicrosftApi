using MassTransit;

namespace Microsoft.Greenlight.Shared.SagaState;

/// <summary>
/// Represents the state of a document ingestion saga.
/// </summary>
public class DocumentIngestionSagaState : SagaStateMachineInstance
{
    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    public string CurrentState { get; set; } = null!;

    /// <summary>
    /// Gets or sets the file hash.
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the original document URL.
    /// </summary>
    public string? OriginalDocumentUrl { get; set; }

    /// <summary>
    /// Gets or sets the OID of the user who uploaded the document.
    /// </summary>
    public string? UploadedByUserOid { get; set; }

    /// <summary>
    /// Gets or sets the classification short code.
    /// </summary>
    public string? ClassificationShortCode { get; set; }

    /// <summary>
    /// Gets or sets the document library short name.
    /// </summary>
    public string? DocumentLibraryShortName { get; set; } = "US.NuclearLicensing";

    /// <summary>
    /// Gets or sets the plugin.
    /// </summary>
    public string? Plugin { get; set; }
}
