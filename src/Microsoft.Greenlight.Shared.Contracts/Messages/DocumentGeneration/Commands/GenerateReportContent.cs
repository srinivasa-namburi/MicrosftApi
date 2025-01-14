using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;

/// <summary>Command to generate report content.</summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record GenerateReportContent(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Generated document JSON.
    /// </summary>
    public string? GeneratedDocumentJson { get; set; }

    /// <summary>
    /// Author OID.
    /// </summary>
    public string? AuthorOid { get; set; }

    /// <summary>
    /// Document process.
    /// </summary>
    public string? DocumentProcess { get; set; }

    /// <summary>
    /// Metadata ID.
    /// </summary>
    public Guid? MetadataId { get; set; }
}
