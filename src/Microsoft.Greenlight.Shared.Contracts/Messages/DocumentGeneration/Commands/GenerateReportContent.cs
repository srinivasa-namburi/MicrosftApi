using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;

/// <summary>Command to generate report content.</summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record GenerateReportContent(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// The JSON representation of the generated document.
    /// </summary>
    /// <remarks>
    /// This is currently only set in GenerateReportContentConsumer, and is set by a non-nullable property.
    /// </remarks>
    public virtual string GeneratedDocumentJson { get; set; } = null!;

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
